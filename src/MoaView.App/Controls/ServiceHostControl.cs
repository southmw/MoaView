using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MoaView.App.Services;
using MoaView.App.Views;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;
using MoaView.Core.Models;
using MoaView.App.Resources;

namespace MoaView.App.Controls;

/// <summary>
/// 서비스 1개(·계정 1개)당 WebView2 1개를 감싼다(04 아키텍처 4.2절).
/// HWND 기반 WebView2를 쓴다(D-06). 위에 WPF 요소를 겹칠 수 없으므로 알림은 MainWindow의 배너가 맡는다.
/// 보안 원칙(10 보안 점검): 로그와 DB에는 URL의 쿼리·프래그먼트를 남기지 않고, 외부 실행은 http(s)만 허용하며,
/// 페이지가 호스트 객체·웹 메시지·외부 프로토콜·장치 권한에 접근하지 못하게 한다.
/// </summary>
public sealed class ServiceHostControl : UserControl, IDisposable
{
    private const string PauseMediaScript =
        "document.querySelectorAll('video').forEach(v => { if (!v.paused) v.pause(); });";

    private const string ExitFullScreenScript =
        "if (document.fullscreenElement && document.exitFullscreen) { document.exitFullscreen(); }";

    /// <summary>FR-PLY-08: 재생 중인 video를 Chromium 내장 PiP 창으로 띄운다. 단축키 입력이 사용자 제스처로 인정된다(09 PoC 결과 P10).</summary>
    private const string PipScript = """
        (async () => {
          try {
            if (document.pictureInPictureElement) { await document.exitPictureInPicture(); return 'exited'; }
            const v = [...document.querySelectorAll('video')].find(x => !x.paused) || document.querySelector('video');
            if (!v || !document.pictureInPictureEnabled || v.disablePictureInPicture) { return 'unavailable'; }
            await v.requestPictureInPicture();
            return 'ok';
          } catch (e) { return 'error:' + e.name; }
        })();
        """;

    public const double MinZoom = 0.5;
    public const double MaxZoom = 3.0;
    private const double ZoomStep = 0.1;

    private readonly IStreamingProvider _provider;
    private readonly Infrastructure.WebView.IWebView2EnvironmentProvider _environmentProvider;
    private readonly ILogger _logger;
    private WebView2 _webView;
    private CancellationTokenSource? _spaLoginDetect;
    private CancellationTokenSource? _settleDetect;
    private static readonly System.Text.RegularExpressions.Regex LoginApiRegex = new(
        @"(login|signin|sign-in|auth|token|session|oauth|passwd|password)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private string? _lastLoginDetectedUrl;
    private DateTime _lastLoginDetectedAt;

    /// <summary>로그인 관련 경로로 POST가 나간 마지막 시각. 자동 입력 뒤 사용자가 실제로 제출했는지 판단하는 데 쓴다.</summary>
    public DateTime? LastLoginPostAt { get; private set; }
    private string? _lastSettledUrl;
    private DateTime _lastSettledAt;

    public StreamingService Service { get; }

    public IStreamingProvider Provider => _provider;

    public string? CurrentUrl { get; private set; }

    public string DocumentTitle { get; private set; } = string.Empty;

    public bool IsReady { get; private set; }

    public event EventHandler<bool>? FullScreenElementChanged;
    public event EventHandler? StateChanged;

    /// <summary>페이지의 파비콘이 바뀌었다(서비스 허용 호스트의 비로그인 페이지일 때만). 받으려면 GetFaviconPngAsync를 호출한다.</summary>
    public event EventHandler? FaviconChanged;
    public event EventHandler<HostFailedEventArgs>? Failed;

    /// <summary>확대 배율이 바뀌었다(저장용).</summary>
    public event EventHandler<double>? ZoomChanged;

    /// <summary>마지막으로 보였던 시각. Suspend·LRU 판단에 쓴다.</summary>
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public double ZoomFactor => _webView.ZoomFactor;

    public bool IsSuspended => !IsDisposed && _webView.CoreWebView2?.IsSuspended == true;

    /// <summary>LRU로 해제된 뒤에는 어떤 WebView 호출도 하지 않는다.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>탐색이 성공적으로 끝났다(로그인 페이지 포함).</summary>
    public event EventHandler<Uri>? PageLoaded;

    /// <summary>
    /// 페이지가 "안정"됐다: DOMContentLoaded가 왔거나, SPA 라우팅으로 URL이 바뀐 뒤 1.5초 동안 그대로다.
    /// NavigationCompleted는 SPA 리디렉션에 취소되거나(Wavve) load 이벤트가 늦어(TVING) 믿을 수 없어서 따로 둔다.
    /// </summary>
    public event EventHandler<Uri>? PageSettled;

    /// <summary>Provider가 로그인 페이지로 판정한 탐색이 끝났다.</summary>
    public event EventHandler<Uri>? LoginPageDetected;

    public ServiceHostControl(
        StreamingService service,
        IStreamingProvider provider,
        Infrastructure.WebView.IWebView2EnvironmentProvider environmentProvider,
        ILogger logger)
    {
        Service = service;
        _provider = provider;
        _environmentProvider = environmentProvider;
        _logger = logger;

        Background = Brushes.Black;
        _webView = CreateWebView();
        Content = _webView;
    }

    /// <summary>
    /// 재시작 복원용으로 저장해도 되는 URL(FR-SVC-07). 로그인·OAuth 페이지(토큰이 포함될 수 있음)나 서비스 밖 호스트면 null.
    /// 프래그먼트는 제거한다.
    /// </summary>
    public string? PersistableUrl
    {
        get
        {
            if (IsDisposed || !Uri.TryCreate(CurrentUrl, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("https" or "http")
                || !HostPattern.MatchesAny(_provider.GetAllowedHosts(Service), uri)
                || _provider.IsLoginPage(Service, uri))
            {
                return null;
            }

            return uri.GetLeftPart(UriPartial.Query);
        }
    }

    public async Task InitializeAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var environment = await _environmentProvider.GetEnvironmentAsync();
            var options = await _environmentProvider.CreateControllerOptionsAsync(Service.ProfileName);
            await _webView.EnsureCoreWebView2Async(environment, options);

            var core = _webView.CoreWebView2;
            ConfigureCore(core);

            _webView.ZoomFactor = Service.ZoomFactor;
            core.Navigate(Service.LastUrl ?? Service.HomeUrl);
            IsReady = true;

            _logger.LogInformation("[{Service}] WebView 초기화 {Elapsed} ms, 프로필 {Profile}",
                Service.Code, stopwatch.ElapsedMilliseconds, Service.ProfileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Service}] WebView 초기화 실패", Service.Code);
            Failed?.Invoke(this, new HostFailedEventArgs(Service, Strings.ServiceOpenFailed, ex.Message, canRetry: true));
        }
    }

    public void GoBack()
    {
        if (_webView.CoreWebView2?.CanGoBack == true)
        {
            _webView.CoreWebView2.GoBack();
        }
    }

    public void GoForward()
    {
        if (_webView.CoreWebView2?.CanGoForward == true)
        {
            _webView.CoreWebView2.GoForward();
        }
    }

    public void Reload() => _webView.CoreWebView2?.Reload();

    public void GoHome() => _webView.CoreWebView2?.Navigate(Service.HomeUrl);

    public void Navigate(string url) => _webView.CoreWebView2?.Navigate(url);

    public bool IsLoginPage(Uri uri) => _provider.IsLoginPage(Service, uri);

    public bool IsAllowedHost(Uri uri) => HostPattern.MatchesAny(_provider.GetAllowedHosts(Service), uri);

    /// <summary>FR-ACC-06: 이 서비스 프로필의 쿠키·저장소를 지운다(로그아웃). 이후 이동은 호출자가 정한다.</summary>
    public async Task ClearSessionAsync()
    {
        if (_webView.CoreWebView2 is null)
        {
            return;
        }

        await _webView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite);
        _logger.LogInformation("[{Service}] 세션(쿠키·사이트 데이터) 삭제", Service.Code);
    }

    /// <summary>
    /// 서비스 홈 도메인의 쿠키 이름 목록(값은 읽지 않는다). 화면으로 로그인 상태를 알 수 없는 서비스(Wavve)의 상태 판정에 쓴다.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetCookieNamesAsync()
    {
        if (IsDisposed || _webView.CoreWebView2 is null)
        {
            return [];
        }

        try
        {
            var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync(Service.HomeUrl);
            return cookies.Select(c => c.Name).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Service}] 쿠키 목록 조회 실패", Service.Code);
            return [];
        }
    }

    /// <summary>스크립트를 실행하고 결과(JSON 문자열)를 돌려준다. 실패하면 null. 스크립트 본문은 로그에 남기지 않는다.</summary>
    public async Task<string?> RunScriptForResultAsync(string script)
    {
        if (IsDisposed || _webView.CoreWebView2 is null)
        {
            return null;
        }

        try
        {
            return await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Service}] 스크립트 실행 실패", Service.Code);
            return null;
        }
    }

    /// <summary>현재 페이지의 파비콘을 PNG로 받는다. 없거나 실패하면 null.</summary>
    public async Task<byte[]?> GetFaviconPngAsync()
    {
        var core = _webView.CoreWebView2;
        if (IsDisposed || core is null || string.IsNullOrEmpty(core.FaviconUri))
        {
            return null;
        }

        try
        {
            using var stream = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.Length > 0 ? buffer.ToArray() : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or IOException)
        {
            _logger.LogDebug(ex, "[{Service}] 파비콘 가져오기 실패", Service.Code);
            return null;
        }
    }

    public Task ExitVideoFullScreenAsync() => RunScriptAsync(ExitFullScreenScript);

    /// <summary>PiP 토글. 비동기 스크립트라 결과는 Promise({})로 오므로 로그만 남긴다.</summary>
    public async Task TogglePictureInPictureAsync()
    {
        await RunScriptAsync(PipScript);
        _logger.LogInformation("[{Service}] PiP 요청", Service.Code);
    }

    public void ZoomIn() => SetZoom(_webView.ZoomFactor + ZoomStep);

    public void ZoomOut() => SetZoom(_webView.ZoomFactor - ZoomStep);

    public void ResetZoom() => SetZoom(1.0);

    public void SetZoom(double factor)
    {
        factor = Math.Round(Math.Clamp(factor, MinZoom, MaxZoom), 2);
        if (Math.Abs(_webView.ZoomFactor - factor) < 0.001)
        {
            return;
        }

        _webView.ZoomFactor = factor;
        Service.ZoomFactor = factor;
        _logger.LogInformation("[{Service}] 확대 배율 {Zoom:0.00}", Service.Code, factor);
        ZoomChanged?.Invoke(this, factor);
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        IsReady = false;
        _spaLoginDetect?.Cancel();
        _settleDetect?.Cancel();
        Content = null;
        _webView.Dispose();
        _logger.LogInformation("[{Service}] WebView 해제(LRU)", Service.Code);
    }

    /// <summary>FR-PLY-04: 다른 서비스로 전환할 때 재생을 멈춘다.</summary>
    public Task PauseMediaAsync() => RunScriptAsync(PauseMediaScript);

    /// <summary>FR-PLY-05: 보이지 않는 상태에서만 성공한다.</summary>
    public async Task<bool> TrySuspendAsync()
    {
        if (_webView.CoreWebView2 is null || Visibility == Visibility.Visible)
        {
            return false;
        }

        var suspended = await _webView.CoreWebView2.TrySuspendAsync();
        _logger.LogInformation("[{Service}] Suspend {Result}", Service.Code, suspended ? "성공" : "거부");
        return suspended;
    }

    public void Resume()
    {
        if (_webView.CoreWebView2?.IsSuspended == true)
        {
            _webView.CoreWebView2.Resume();
            _logger.LogInformation("[{Service}] Resume", Service.Code);
        }
    }

    private async Task RunScriptAsync(string script)
    {
        if (IsDisposed || _webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Service}] 스크립트 실행 실패", Service.Code);
        }
    }

    private static WebView2 CreateWebView() => new()
    {
        DefaultBackgroundColor = System.Drawing.Color.Black,
        AllowExternalDrop = false,
    };

    private void ConfigureCore(CoreWebView2 core)
    {
        var settings = core.Settings;
        settings.IsStatusBarEnabled = false;
        settings.AreDefaultContextMenusEnabled = true;
        settings.IsZoomControlEnabled = true;

        // 페이지가 호스트 프로세스에 닿는 통로를 닫는다. 자동 입력도 ExecuteScriptAsync 반환값만 쓰므로 웹 메시지는 항상 끈다.
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = false;

        // 브라우저의 비밀번호 저장·자동 완성은 쓰지 않는다. 자격증명은 앱이 DPAPI로 관리한다(FR-ACC-02).
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
#if !DEBUG
        settings.AreDevToolsEnabled = false;
#endif
        if (!string.IsNullOrWhiteSpace(Service.UserAgentOverride))
        {
            settings.UserAgent = Service.UserAgentOverride;
        }

        core.ContainsFullScreenElementChanged += (_, _) =>
        {
            _logger.LogInformation("[{Service}] 영상 전체화면 {State}", Service.Code, core.ContainsFullScreenElement ? "진입" : "해제");
            FullScreenElementChanged?.Invoke(this, core.ContainsFullScreenElement);
        };

        core.SourceChanged += (_, _) =>
        {
            CurrentUrl = core.Source;
            _logger.LogDebug("[{Service}] URL 변경: {Url}", Service.Code, SanitizeForLog(core.Source));
            StateChanged?.Invoke(this, EventArgs.Empty);
            ScheduleSpaLoginDetection(core.Source);
            ScheduleSettle(core.Source);
        };

        core.DOMContentLoaded += (_, _) => RaisePageSettled(core.Source, "DOM");

        // 로그인 제출 관찰: 로그인·인증 경로로 나가는 POST만 기록한다(URL·본문은 로그에 남기지 않는다).
        core.WebResourceResponseReceived += (_, e) =>
        {
            // 서비스 도메인(또는 로그인 제공자 도메인)으로 나가는 것만 인정한다. 분석 서비스(google.com 등)의 POST는 제외.
            if (string.Equals(e.Request.Method, "POST", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var requestUri)
                && (IsAllowedHost(requestUri) || IsLoginPage(requestUri))
                && LoginApiRegex.IsMatch(requestUri.AbsolutePath))
            {
                LastLoginPostAt = DateTime.UtcNow;
                _logger.LogDebug("[{Service}] 로그인 관련 POST 관찰: {Host}", Service.Code, requestUri.Host);
            }
        };

        core.DocumentTitleChanged += (_, _) =>
        {
            DocumentTitle = core.DocumentTitle;
            StateChanged?.Invoke(this, EventArgs.Empty);
        };

        // FR-SVC-06: 로그인·OAuth 페이지(제공자 아이콘)나 서비스 밖 페이지의 파비콘은 서비스 아이콘으로 쓰지 않는다.
        core.FaviconChanged += (_, _) =>
        {
            if (string.IsNullOrEmpty(core.FaviconUri)
                || !Uri.TryCreate(core.Source, UriKind.Absolute, out var uri)
                || !IsAllowedHost(uri) || IsLoginPage(uri))
            {
                return;
            }

            FaviconChanged?.Invoke(this, EventArgs.Empty);
        };

        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.NewWindowRequested += OnNewWindowRequested;
        core.PermissionRequested += OnPermissionRequested;
        core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
        core.DownloadStarting += OnDownloadStarting;
        core.ProcessFailed += OnProcessFailed;
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        _logger.LogDebug("[{Service}] 탐색 시작: {Url}", Service.Code, SanitizeForLog(e.Uri));

        // Phase 1은 도메인 밖 탐색을 막지 않고 기록만 한다. PoC에서 Google 로그인이 accounts.google.co.kr 등 예상 밖 호스트를
        // 거치는 것을 확인했으므로, 차단 목록은 Phase 2에서 로그를 근거로 확정한다(06 UI/UX 2.1절).
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)
            && !HostPattern.MatchesAny(_provider.GetAllowedHosts(Service), uri))
        {
            _logger.LogInformation("[{Service}] 허용 호스트 밖 탐색: {Host}", Service.Code, uri.Host);
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var core = _webView.CoreWebView2!;
        _logger.LogDebug("[{Service}] 탐색 완료 성공={Success} 상태={Status}: {Url}",
            Service.Code, e.IsSuccess, e.WebErrorStatus, SanitizeForLog(core.Source));
        if (!e.IsSuccess)
        {
            // ConnectionAborted는 SPA 리디렉션에서 흔히 생기는 정상 취소다(09 PoC 결과 관찰).
            if (e.WebErrorStatus is CoreWebView2WebErrorStatus.ConnectionAborted
                or CoreWebView2WebErrorStatus.OperationCanceled)
            {
                return;
            }

            _logger.LogWarning("[{Service}] 탐색 실패 {Status}: {Url}", Service.Code, e.WebErrorStatus, SanitizeForLog(core.Source));
            Failed?.Invoke(this, new HostFailedEventArgs(Service, Strings.ConnectionFailed,
                $"{e.WebErrorStatus} ({SanitizeForLog(core.Source)})", canRetry: true));
            return;
        }

        if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var uri))
        {
            return;
        }

        PageLoaded?.Invoke(this, uri);
        RaiseLoginPageDetected(uri, "탐색");
    }

    /// <summary>
    /// Google처럼 pushState로 단계(이메일 → 비밀번호)를 바꾸는 로그인은 NavigationCompleted가 오지 않는다.
    /// SourceChanged 뒤 잠시 기다렸다가 같은 URL이면 로그인 페이지 감지를 올린다.
    /// </summary>
    private void ScheduleSpaLoginDetection(string source)
    {
        _spaLoginDetect?.Cancel();
        _spaLoginDetect?.Dispose();
        _spaLoginDetect = null;

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || !_provider.IsLoginPage(Service, uri))
        {
            // 로그인 페이지를 벗어났다. 다음에 다시 로그인 페이지가 오면(재로그아웃 등) 새로 감지해야 한다.
            _lastLoginDetectedUrl = null;
            return;
        }

        var cts = new CancellationTokenSource();
        _spaLoginDetect = cts;
        _ = DetectLaterAsync(uri, cts.Token);

        async Task DetectLaterAsync(Uri target, CancellationToken token)
        {
            try
            {
                await Task.Delay(900, token);
                if (string.Equals(CurrentUrl, target.AbsoluteUri, StringComparison.Ordinal))
                {
                    RaiseLoginPageDetected(target, "SPA");
                }
            }
            catch (OperationCanceledException)
            {
                // 더 새로운 URL로 대체됨
            }
        }
    }

    private void ScheduleSettle(string source)
    {
        _settleDetect?.Cancel();
        _settleDetect?.Dispose();
        var cts = new CancellationTokenSource();
        _settleDetect = cts;
        _ = SettleLaterAsync(source, cts.Token);

        async Task SettleLaterAsync(string target, CancellationToken token)
        {
            try
            {
                await Task.Delay(1500, token);
                if (string.Equals(CurrentUrl, target, StringComparison.Ordinal))
                {
                    RaisePageSettled(target, "SPA");
                }
            }
            catch (OperationCanceledException)
            {
                // 더 새로운 URL로 대체됨
            }
        }
    }

    /// <summary>같은 URL은 3초 안에 한 번만 올린다(DOM과 SPA 안정화가 겹치지 않게).</summary>
    private void RaisePageSettled(string? source, string origin)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (string.Equals(_lastSettledUrl, source, StringComparison.Ordinal) && now - _lastSettledAt < TimeSpan.FromSeconds(3))
        {
            return;
        }

        _lastSettledUrl = source;
        _lastSettledAt = now;
        _logger.LogDebug("[{Service}] 페이지 안정({Origin}): {Url}", Service.Code, origin, SanitizeForLog(source));
        PageSettled?.Invoke(this, uri);
    }

    /// <summary>같은 URL(쿼리 제외)은 3초 안에 한 번만 올린다. 탐색 완료와 SPA 감지가 겹치지 않게 한다.</summary>
    private void RaiseLoginPageDetected(Uri uri, string source)
    {
        if (!_provider.IsLoginPage(Service, uri))
        {
            return;
        }

        var key = uri.GetLeftPart(UriPartial.Path);
        var now = DateTime.UtcNow;
        if (string.Equals(_lastLoginDetectedUrl, key, StringComparison.Ordinal) && now - _lastLoginDetectedAt < TimeSpan.FromSeconds(3))
        {
            return;
        }

        _lastLoginDetectedUrl = key;
        _lastLoginDetectedAt = now;
        _logger.LogInformation("[{Service}] 로그인 페이지 감지({Source}): {Url}", Service.Code, source, SanitizeForLog(uri.AbsoluteUri));
        LoginPageDetected?.Invoke(this, uri);
    }

    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var core = _webView.CoreWebView2!;
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target))
        {
            e.Handled = true;
            return;
        }

        var policy = _provider.ResolveNewWindow(Service, target);
        _logger.LogInformation("[{Service}] 새 창 요청 {Host} → {Policy}", Service.Code, target.Host, policy);

        switch (policy)
        {
            case NewWindowPolicy.InApp:
                e.Handled = true;
                core.Navigate(e.Uri);
                break;

            case NewWindowPolicy.OAuthDialog:
                var deferral = e.GetDeferral();
                try
                {
                    var popup = new OAuthPopupWindow { Owner = Window.GetWindow(this) };
                    popup.Show();
                    var environment = await _environmentProvider.GetEnvironmentAsync();
                    var options = await _environmentProvider.CreateControllerOptionsAsync(Service.ProfileName);
                    var popupCore = await popup.InitializeAsync(environment, options);
                    e.NewWindow = popupCore;
                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Service}] OAuth 팝업 생성 실패. 같은 창에서 연다", Service.Code);
                    e.Handled = true;
                    core.Navigate(e.Uri);
                }
                finally
                {
                    deferral.Complete();
                }
                break;

            case NewWindowPolicy.ExternalBrowser:
                e.Handled = true;
                OpenInDefaultBrowser(target);
                break;
        }
    }

    /// <summary>기본 브라우저로 넘길 때는 http(s)만 허용한다. 그 밖의 스킴은 임의 프로토콜 핸들러 실행으로 이어질 수 있다.</summary>
    private void OpenInDefaultBrowser(Uri target)
    {
        if (target.Scheme is not ("https" or "http"))
        {
            _logger.LogWarning("[{Service}] 외부 실행 거부(허용되지 않은 스킴 {Scheme})", Service.Code, target.Scheme);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Service}] 기본 브라우저 실행 실패", Service.Code);
        }
    }

    /// <summary>장치·위치·알림 권한은 모두 거부한다. 재생에 필요한 자동 재생만 허용한다. 오버레이 UI가 없어 사용자에게 물을 수도 없다.</summary>
    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        e.State = e.PermissionKind == CoreWebView2PermissionKind.Autoplay
            ? CoreWebView2PermissionState.Allow
            : CoreWebView2PermissionState.Deny;
        e.Handled = true;
        _logger.LogInformation("[{Service}] 권한 요청 {Kind} → {State}", Service.Code, e.PermissionKind, e.State);
    }

    /// <summary>페이지가 ms-settings:, 사용자 지정 프로토콜 등 외부 앱을 띄우려는 시도는 막는다(D-07: 재생을 외부로 넘기지 않음).</summary>
    private void OnLaunchingExternalUriScheme(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        e.Cancel = true;
        _logger.LogWarning("[{Service}] 외부 프로토콜 실행 차단: {Uri}", Service.Code, SanitizeForLog(e.Uri));
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        // 다운로드는 기본 UI로 허용하되(치지직 커넥터 설치 파일 등) 기록을 남긴다. 저장 경로는 WebView2 기본값이다.
        _logger.LogInformation("[{Service}] 다운로드 시작: {File} ({Bytes} bytes)",
            Service.Code, System.IO.Path.GetFileName(e.ResultFilePath), e.DownloadOperation.TotalBytesToReceive);
    }

    private async void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _logger.LogError("[{Service}] 프로세스 실패 {Kind} 이유={Reason} 종료코드={ExitCode}",
            Service.Code, e.ProcessFailedKind, e.Reason, e.ExitCode);

        switch (e.ProcessFailedKind)
        {
            case CoreWebView2ProcessFailedKind.RenderProcessExited:
            case CoreWebView2ProcessFailedKind.RenderProcessUnresponsive:
            case CoreWebView2ProcessFailedKind.FrameRenderProcessExited:
                // NFR-07: 해당 서비스만 다시 불러오고 앱은 유지한다.
                Failed?.Invoke(this, new HostFailedEventArgs(Service, Strings.PageReloadingAfterCrash,
                    e.ProcessFailedKind.ToString(), canRetry: false));
                _webView.CoreWebView2?.Reload();
                break;

            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                await RecreateAsync();
                break;
        }
    }

    /// <summary>로그용 URL: 스킴·호스트·경로만 남기고 쿼리·프래그먼트(세션 토큰이 들어올 수 있음)는 버린다(NFR-05).</summary>
    internal static string SanitizeForLog(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var cut = url.IndexOfAny(['?', '#']);
            return cut < 0 ? url : url[..cut] + "?…";
        }

        var path = uri.GetLeftPart(UriPartial.Path);
        return string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) ? path : path + "?…";
    }

    /// <summary>브라우저 프로세스가 죽으면 컨트롤을 새로 만들고 마지막 URL을 복원한다.</summary>
    private async Task RecreateAsync()
    {
        IsReady = false;
        Content = null;
        _webView.Dispose();
        _webView = CreateWebView();
        Content = _webView;
        await InitializeAsync();
    }
}
