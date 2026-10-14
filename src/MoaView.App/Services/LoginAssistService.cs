using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoaView.App.Controls;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;
using MoaView.Core.Models;
using MoaView.Infrastructure.Configuration;

namespace MoaView.App.Services;

/// <summary>사용자에게 보여 줄 로그인 관련 알림(06 UI/UX 3.2절 배너).</summary>
public sealed record LoginNotice(
    string Title,
    string Message,
    string? ActionText = null,
    Func<Task>? Action = null,
    string? SecondaryText = null,
    Func<Task>? SecondaryAction = null);

/// <summary>
/// 로그인 보조(04 아키텍처 5.3절, D-09): 만료 감지 → 자동 입력 → (옵션) 자동 제출 → 결과 판정.
/// 복호화한 비밀번호는 스크립트 호출 직전에만 만들고 바로 버린다. 로그·알림에는 넣지 않는다(10 보안 점검 3절).
/// </summary>
public sealed class LoginAssistService(
    IServiceHostManager hosts,
    IAccountRepository accounts,
    ICredentialProtector protector,
    IAccountsDialog accountsDialog,
    IAppSettingsStore settings,
    IOptions<LoginOptions> options,
    ILogger<LoginAssistService> logger)
{
    private const string CookieSignatureKeyPrefix = SettingKeys.CookieSignaturePrefix;

    private readonly HashSet<string> _noAccountDismissed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _busy = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>로그인 페이지에 있을 때(로그아웃 상태) 본 쿠키 이름. 로그인 뒤 쿠키와의 차이가 "로그인 서명"이 된다.</summary>
    private readonly Dictionary<string, IReadOnlyList<string>> _loggedOutCookieBaseline = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>자동 입력을 했고 사용자가 로그인을 마치기를 기다리는 서비스. 다음 일반 페이지에서 서명을 학습한다.</summary>
    private readonly HashSet<string> _awaitingLoginConfirm = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>자동 입력 시각. 이후에 로그인 POST가 관찰되면 제출로 본다.</summary>
    private readonly Dictionary<string, DateTime> _filledAt = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>로그아웃 상태를 보고 로그인 페이지로 보낸 서비스. 로그인된 것이 확인되면 지운다(반복 리디렉션 방지).</summary>
    private readonly HashSet<string> _redirectedToLogin = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<LoginNotice>? Notice;

    public void Start() => hosts.HostCreated += OnHostCreated;

    /// <summary>사용자가 앱에서 로그아웃(쿠키 삭제)한 직후. 저장된 계정이 있으면 바로 로그인 페이지로, 없으면 홈으로 간다.</summary>
    public async Task AfterLogoutAsync(StreamingService service)
    {
        var host = hosts.GetHost(service.Code);
        if (host is null)
        {
            return;
        }

        _redirectedToLogin.Remove(service.Code);
        var account = await accounts.GetDefaultAsync(service.Id);
        var loginUrl = host.Provider.GetLoginUrl(service);
        if (account is { AutoFillEnabled: true } && loginUrl is not null)
        {
            _redirectedToLogin.Add(service.Code);
            logger.LogInformation("[{Service}] 로그아웃 후 로그인 페이지로 이동", service.Code);
            host.Navigate(loginUrl);
        }
        else
        {
            host.GoHome();
        }
    }

    private readonly HashSet<string> _checkingLoggedOut = new(StringComparer.OrdinalIgnoreCase);

    private void OnHostCreated(object? sender, ServiceHostControl host)
    {
        host.LoginPageDetected += (_, uri) => _ = HandleLoginPageAsync(host, uri);
        host.PageSettled += (_, uri) => _ = HandlePageSettledAsync(host, uri);
    }

    // ---------------------------------------------------------------- 로그인 페이지: 자동 입력 → 자동 제출

    private async Task HandleLoginPageAsync(ServiceHostControl host, Uri uri)
    {
        var service = host.Service;
        if (host.IsDisposed || uri.Scheme != "https")
        {
            return;
        }

        if (!_busy.Add(service.Code))
        {
            logger.LogDebug("[{Service}] 로그인 보조가 이미 진행 중이라 건너뜀", service.Code);
            return;
        }

        try
        {
            var account = await accounts.GetDefaultAsync(service.Id);
            if (account is null)
            {
                OfferToSaveAccount(service);
                return;
            }

            // 로그인 페이지 = 로그아웃 상태. 이때의 쿠키 이름을 기준선으로 둔다(쿠키 서명 학습).
            _loggedOutCookieBaseline[service.Code] = await host.GetCookieNamesAsync();

            if (!account.AutoFillEnabled || account.EncryptedPassword is null)
            {
                logger.LogInformation("[{Service}] 자동 입력 건너뜀(비활성 또는 비밀번호 없음)", service.Code);
                return;
            }

            var isNative = string.Equals(account.LoginProvider, LoginProviders.Native, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(account.LoginProvider, host.Provider.PrimaryLoginProvider, StringComparison.OrdinalIgnoreCase);
            if (!isNative)
            {
                // 소셜·제휴 로그인(서비스 화면에서 제공자 버튼을 눌러야 하는 경우)은 순차 지원(FR-ACC-11).
                logger.LogInformation("[{Service}] {Provider} 로그인 자동 입력은 아직 지원하지 않음", service.Code, account.LoginProvider);
                return;
            }

            var forms = host.Provider.GetLoginFormSpec(service)?.Forms;
            var form = forms?.GetValueOrDefault(account.LoginProvider) ?? forms?.GetValueOrDefault(LoginProviders.Native);
            var fillResult = await FillWithRetryAsync(host, account, form, allowIdOnly: true);
            logger.LogInformation("[{Service}] 자동 입력 결과: {Result}", service.Code, fillResult);

            if (fillResult == "id-only")
            {
                // 이메일 먼저 받는 2단계 로그인(Google 등): '다음'을 누른 뒤 비밀번호 입력란이 나타날 때까지 기다렸다가 채운다.
                await RunAutofillAsync(host, account, form, "submit");
                await Task.Delay(800);
                fillResult = await FillWithRetryAsync(host, account, form, allowIdOnly: false);
                logger.LogInformation("[{Service}] 2단계 자동 입력 결과: {Result}", service.Code, fillResult);
            }

            if (fillResult is not ("filled" or "filled-password-only"))
            {
                return;
            }

            _awaitingLoginConfirm.Add(service.Code);
            _filledAt[service.Code] = DateTime.UtcNow;

            var verdict = LoginGuard.Evaluate(account, DateTime.UtcNow, Cooldown, options.Value.MaxConsecutiveFailures);
            if (verdict != LoginGuard.Verdict.Allowed)
            {
                logger.LogInformation("[{Service}] 자동 제출 안 함: {Verdict}", service.Code, verdict);
                if (verdict != LoginGuard.Verdict.AutoSubmitDisabled)
                {
                    Raise(new LoginNotice("자동 제출을 잠시 멈췄습니다",
                        verdict == LoginGuard.Verdict.TooManyFailures
                            ? "자동 로그인이 연속으로 실패해 자동 제출을 껐습니다. 비밀번호를 확인해 주세요."
                            : "최근에 자동 제출을 시도해서 잠시 기다립니다. 로그인 버튼을 직접 눌러 주세요.",
                        "계정 수정", () => accountsDialog.ShowAsync(service)));
                }
                else
                {
                    Raise(new LoginNotice("로그인이 끊겨서 저장된 계정으로 입력했습니다",
                        "로그인 버튼을 눌러 주세요. 자동 제출은 계정 설정에서 켤 수 있습니다.",
                        "계정 관리", () => accountsDialog.ShowAsync(service)));
                }

                return;
            }

            account.LastAutoSubmitAt = DateTime.UtcNow;
            await accounts.SaveAsync(account);

            var submitResult = await RunAutofillAsync(host, account, form, "submit");
            logger.LogInformation("[{Service}] 자동 제출: {Result}", service.Code, submitResult);

            var succeeded = await WaitForLoginResultAsync(host);
            var disabled = LoginGuard.RecordResult(account, succeeded, DateTime.UtcNow, options.Value.MaxConsecutiveFailures);
            await accounts.SaveAsync(account);

            if (succeeded)
            {
                _redirectedToLogin.Remove(service.Code);
                logger.LogInformation("[{Service}] 자동 로그인 성공", service.Code);
                await LearnCookieSignatureAsync(host);
            }
            else
            {
                logger.LogWarning("[{Service}] 자동 로그인 실패 (연속 {Count}회{Disabled})",
                    service.Code, account.LoginFailCount, disabled ? ", 자동 제출 끔" : string.Empty);
                Raise(new LoginNotice("자동 로그인에 실패했습니다",
                    disabled ? "연속 실패로 자동 제출을 껐습니다. 비밀번호를 확인해 주세요." : "비밀번호를 확인해 주세요.",
                    "계정 수정", () => accountsDialog.ShowAsync(service)));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Service}] 로그인 보조 중 오류", service.Code);
        }
        finally
        {
            _busy.Remove(service.Code);
        }
    }

    private TimeSpan Cooldown => TimeSpan.FromMinutes(options.Value.AutoSubmitCooldownMinutes);

    private void OfferToSaveAccount(StreamingService service)
    {
        if (_noAccountDismissed.Contains(service.Code))
        {
            return;
        }

        Raise(new LoginNotice("이 서비스의 계정을 저장하면 다음부터 자동으로 입력합니다",
            $"{service.Name} 로그인 정보는 이 PC에만 암호화되어 저장됩니다.",
            "계정 저장", () => accountsDialog.ShowAsync(service),
            "다시 보지 않기", () =>
            {
                _noAccountDismissed.Add(service.Code);
                return Task.CompletedTask;
            }));
    }

    /// <summary>SPA가 폼을 늦게 그릴 수 있어 제한 시간 동안 재시도한다. allowIdOnly=false면 비밀번호 입력란이 나올 때까지 기다린다.</summary>
    private async Task<string> FillWithRetryAsync(ServiceHostControl host, Account account, LoginForm? form, bool allowIdOnly)
    {
        var deadline = DateTime.UtcNow.AddSeconds(options.Value.FillRetrySeconds);
        var allowEntryClick = true;
        var result = "no-form";
        while (true)
        {
            result = await RunAutofillAsync(host, account, form, "fill", allowEntryClick);
            if (result == "entry-clicked")
            {
                // 로그인 방식 선택 화면에서 자체 ID 버튼을 눌렀다. 폼이 그려질 시간을 더 주고, 버튼은 다시 누르지 않는다.
                logger.LogInformation("[{Service}] 로그인 방식 선택 화면에서 자체 ID 로그인으로 진입", host.Service.Code);
                allowEntryClick = false;
                deadline = DateTime.UtcNow.AddSeconds(options.Value.FillRetrySeconds);
                await Task.Delay(1000);
                continue;
            }

            var done = result is "filled" or "filled-password-only" || (allowIdOnly && result == "id-only");
            if (done || DateTime.UtcNow >= deadline)
            {
                return result;
            }

            await Task.Delay(700);
        }
    }

    private async Task<string> RunAutofillAsync(ServiceHostControl host, Account account, LoginForm? form, string action, bool allowEntryClick = false)
    {
        // 현재 페이지가 서비스 허용 호스트이거나 Provider가 로그인 페이지로 판정한 곳일 때만 값을 넣는다.
        if (!Uri.TryCreate(host.CurrentUrl, UriKind.Absolute, out var current)
            || !(host.IsAllowedHost(current) || host.IsLoginPage(current)))
        {
            return "no-form";
        }

        var password = action == "fill" ? protector.Unprotect(account.EncryptedPassword!) : string.Empty;
        var script = ScriptLoader.Load("autofill.js", new
        {
            action,
            loginId = account.LoginId,
            password,
            idSelector = form?.IdSelector,
            passwordSelector = form?.PasswordSelector,
            submitSelector = form?.SubmitSelector,
            entrySelector = form?.EntrySelector,
            allowEntryClick,
        });
        password = string.Empty;

        var raw = await host.RunScriptForResultAsync(script);
        return ScriptLoader.ReadString(raw) ?? "no-form";
    }

    /// <summary>자동 제출 뒤 로그인 페이지를 벗어나면 성공, 제한 시간 안에 못 벗어나면 실패.</summary>
    private async Task<bool> WaitForLoginResultAsync(ServiceHostControl host)
    {
        var deadline = DateTime.UtcNow.AddSeconds(options.Value.SubmitJudgeSeconds);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(1000);
            if (Uri.TryCreate(host.CurrentUrl, UriKind.Absolute, out var uri) && !host.IsLoginPage(uri))
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- 일반 페이지: 로그아웃 표식 감지

    private async Task HandlePageSettledAsync(ServiceHostControl host, Uri uri)
    {
        var service = host.Service;
        if (host.IsDisposed || _busy.Contains(service.Code) || host.IsLoginPage(uri) || !host.IsAllowedHost(uri))
        {
            return;
        }

        var loginUrl = host.Provider.GetLoginUrl(service);
        if (loginUrl is null || !_checkingLoggedOut.Add(service.Code))
        {
            return;
        }

        try
        {
            var account = await accounts.GetDefaultAsync(service.Id);
            if (account is null || !account.AutoFillEnabled)
            {
                return;
            }

            // DB 셀렉터(loggedOutMarker)가 있으면 그것으로, 없으면 로그인/로그아웃 링크 휴리스틱으로 판단한다.
            var marker = host.Provider.GetLoginFormSpec(service)?.LoggedOutMarker;
            var script = ScriptLoader.Load("logged-out-check.js", new { marker });
            var state = "unknown";

            // 자동 입력 뒤 처음 보는 일반 페이지. 로그인 페이지에서 실제 제출(버튼·Enter)이 있었을 때만 "로그인 완료"로 보고
            // 바로 쿠키 서명을 학습한다(오래 기다리면 그 사이 로그아웃되어 쿠키가 지워질 수 있다).
            // 제출 없이 돌아온 경우(홈 버튼 등)는 로그아웃 상태일 수 있으므로 아래 일반 판정으로 넘어간다.
            if (_awaitingLoginConfirm.Remove(service.Code))
            {
                await Task.Delay(1500);
                if (!string.Equals(host.CurrentUrl, uri.AbsoluteUri, StringComparison.Ordinal))
                {
                    _awaitingLoginConfirm.Add(service.Code);
                    return;
                }

                // 제출 판단: (1) 로그인 페이지에서 자동 입력 이후 로그인 관련 POST가 관찰됐거나 (2) 페이지 저장소의 제출 표시가 있으면 제출로 본다.
                var postObserved = _filledAt.TryGetValue(service.Code, out var filledAt)
                                   && host.LastLoginPostAt is { } postAt && postAt > filledAt;
                var submittedAgeRaw = await host.RunScriptForResultAsync(ScriptLoader.Load("login-submitted-check.js"));
                var flagObserved = double.TryParse(submittedAgeRaw, out var age) && age >= 0 && age < 120_000;
                var submitted = postObserved || flagObserved;
                logger.LogDebug("[{Service}] 로그인 제출 판단: POST={Post}, 표시={Flag}", service.Code, postObserved, flagObserved);
                if (submitted)
                {
                    state = ScriptLoader.ReadString(await host.RunScriptForResultAsync(script)) ?? "unknown";
                    if (state != "logged-out" && await LearnCookieSignatureAsync(host))
                    {
                        _redirectedToLogin.Remove(service.Code);
                        return;
                    }
                }
                else
                {
                    logger.LogDebug("[{Service}] 로그인 제출 없이 일반 페이지로 돌아옴. 로그인 상태를 다시 판정", service.Code);
                }
            }

            // 쿠키 서명을 이미 학습한 서비스(Wavve처럼 화면으로 판단이 안 되는 곳)는 화면 검사보다 먼저, 빠르게 판정한다.
            await Task.Delay(1500);
            if (!string.Equals(host.CurrentUrl, uri.AbsoluteUri, StringComparison.Ordinal))
            {
                return;
            }

            state = await JudgeByCookieSignatureAsync(host);

            // SPA가 헤더를 늦게 그리므로 결과가 확실해질 때까지 몇 번 다시 본다. 그 사이 다른 페이지로 갔으면 그만둔다.
            foreach (var delayMs in state == "unknown" ? new[] { 0, 2000, 2500, 3000 } : [])
            {
                await Task.Delay(delayMs);
                if (!string.Equals(host.CurrentUrl, uri.AbsoluteUri, StringComparison.Ordinal))
                {
                    return;
                }

                state = ScriptLoader.ReadString(await host.RunScriptForResultAsync(script)) ?? "unknown";
                if (state != "unknown")
                {
                    break;
                }
            }

            if (!string.Equals(host.CurrentUrl, uri.AbsoluteUri, StringComparison.Ordinal))
            {
                return;
            }

            if (state == "unknown" && marker is null)
            {
                // 마우스를 올려야 메뉴가 생기는 헤더: 트리거에 마우스 진입 이벤트를 보내 잠깐 열고 확인한 뒤 닫는다.
                var opened = await host.RunScriptForResultAsync(ScriptLoader.Load("hover-probe.js", new { action = "open" }));
                if (opened is not null and not "0")
                {
                    await Task.Delay(600);
                    state = ScriptLoader.ReadString(await host.RunScriptForResultAsync(script)) ?? "unknown";
                    await host.RunScriptForResultAsync(ScriptLoader.Load("hover-probe.js", new { action = "close" }));
                    logger.LogDebug("[{Service}] hover 탐침 결과: {State} (트리거 {Count}개)", service.Code, state, opened);
                }
            }

            if (state == "unknown")
            {
                state = await JudgeByCookieSignatureAsync(host);
            }

            if (state == "logged-in")
            {
                // 로그인된 상태가 확인됐으므로 다음에 끊기면 다시 보낼 수 있다.
                _redirectedToLogin.Remove(service.Code);
                return;
            }

            if (state != "logged-out")
            {
                logger.LogDebug("[{Service}] 로그인 상태를 판단하지 못함", service.Code);
                return;
            }

            if (_redirectedToLogin.Contains(service.Code))
            {
                // 이미 보냈는데 사용자가 로그인하지 않고 돌아온 것. 강제로 다시 보내지 않고 배너로만 알린다(반복 이동 방지).
                logger.LogInformation("[{Service}] 로그아웃 상태(재감지). 배너로 안내", service.Code);
                Raise(new LoginNotice("로그아웃 상태입니다", $"{service.Name}에 저장된 계정으로 로그인할 수 있습니다.",
                    "로그인", () =>
                    {
                        host.Navigate(loginUrl);
                        return Task.CompletedTask;
                    }));
                return;
            }

            _redirectedToLogin.Add(service.Code);
            logger.LogInformation("[{Service}] 로그아웃 상태 감지({Mode}) → 로그인 페이지로 이동",
                service.Code, marker is null ? "휴리스틱" : "표식");
            host.Navigate(loginUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{Service}] 로그아웃 상태 확인 실패", service.Code);
        }
        finally
        {
            _checkingLoggedOut.Remove(service.Code);
        }
    }

    // ---------------------------------------------------------------- 쿠키 서명 학습 (화면으로 판단할 수 없는 서비스용)

    /// <summary>로그인 뒤 쿠키 이름 − 로그아웃 기준선 = 로그인 서명. 서비스별로 AppSettings에 저장한다(이름만, 값 없음). 학습했으면 true.</summary>
    private async Task<bool> LearnCookieSignatureAsync(ServiceHostControl host)
    {
        var code = host.Service.Code;
        if (!_loggedOutCookieBaseline.TryGetValue(code, out var baseline))
        {
            return false;
        }

        var current = await host.GetCookieNamesAsync();
        if (current.Count == 0)
        {
            // 방금 세션이 지워진 상태 등. 다음 일반 페이지에서 다시 시도한다.
            logger.LogDebug("[{Service}] 쿠키 서명 학습 보류(쿠키 없음)", code);
            return false;
        }

        // 분석·광고 쿠키는 로그인과 무관하게 생기므로 서명에서 뺀다.
        var signature = current.Except(baseline, StringComparer.Ordinal).Where(n => !IsAnalyticsCookie(n)).ToList();
        if (signature.Count == 0)
        {
            logger.LogDebug("[{Service}] 쿠키 서명 학습 실패(로그인 전후 쿠키 이름이 같음)", code);
            return false;
        }

        await settings.SetAsync(CookieSignatureKeyPrefix + code, signature);
        logger.LogInformation("[{Service}] 로그인 쿠키 서명 학습: {Count}개 이름", code, signature.Count);
        return true;
    }

    private async Task<string> JudgeByCookieSignatureAsync(ServiceHostControl host)
    {
        var code = host.Service.Code;
        var signature = await settings.GetAsync<List<string>>(CookieSignatureKeyPrefix + code);
        if (signature is null || signature.Count == 0)
        {
            return "unknown";
        }

        var current = await host.GetCookieNamesAsync();
        var meaningful = signature.Where(n => !IsAnalyticsCookie(n)).ToList();
        if (meaningful.Count == 0)
        {
            return "unknown";
        }

        // 과반이 있으면 로그인, 과반 미만이면 로그아웃. 일부 쿠키가 만료·재생성돼도 흔들리지 않게 한다.
        var present = meaningful.Count(name => current.Contains(name, StringComparer.Ordinal));
        var state = present * 2 >= meaningful.Count ? "logged-in" : "logged-out";
        logger.LogDebug("[{Service}] 쿠키 서명 판정: {State} ({Present}/{Total})", code, state, present, meaningful.Count);
        return state;
    }

    private static bool IsAnalyticsCookie(string name) =>
        name.StartsWith("_dd_", StringComparison.OrdinalIgnoreCase)      // Datadog
        || name.StartsWith("_ga", StringComparison.OrdinalIgnoreCase)    // Google Analytics
        || name.StartsWith("_gid", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("_gcl", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("_fbp", StringComparison.OrdinalIgnoreCase)   // Meta
        || name.StartsWith("_fbc", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("ab.storage", StringComparison.OrdinalIgnoreCase) // Braze
        || name.StartsWith("amp_", StringComparison.OrdinalIgnoreCase)   // Amplitude
        || name.StartsWith("_hj", StringComparison.OrdinalIgnoreCase)    // Hotjar
        || name.StartsWith("ajs_", StringComparison.OrdinalIgnoreCase)   // Segment
        || name.StartsWith("_tt_", StringComparison.OrdinalIgnoreCase)   // TikTok
        || name.StartsWith("wcs_", StringComparison.OrdinalIgnoreCase)   // Naver Analytics
        || name.Equals("NID", StringComparison.Ordinal)
        || name.Equals("_clck", StringComparison.Ordinal) || name.Equals("_clsk", StringComparison.Ordinal); // Clarity

    private void Raise(LoginNotice notice) => Notice?.Invoke(this, notice);
}
