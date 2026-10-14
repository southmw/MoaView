using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MoaView.App.Services;
using MoaView.App.Views;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;
using MoaView.Core.Models;
using MoaView.App.Resources;

namespace MoaView.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IServiceCatalog _catalog;
    private readonly IServiceHostManager _hosts;
    private readonly IWindowService _window;
    private readonly IAppSettingsStore _settings;
    private readonly IAccountsDialog _accountsDialog;
    private readonly ISettingsDialog _settingsDialog;
    private readonly IServiceRepository _serviceRepository;
    private readonly FaviconCache _favicons;
    private readonly UpdateService _updates;
    private readonly LoginAssistService _loginAssist;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>시작 직후에는 서비스 로드가 우선이라 업데이트 확인은 조금 미룬다.</summary>
    private static readonly TimeSpan UpdateCheckDelay = TimeSpan.FromSeconds(10);

    private Func<Task>? _bannerAction;
    private Func<Task>? _bannerSecondaryAction;
    private bool _isInitialized;

    public MainViewModel(
        IServiceCatalog catalog,
        IServiceHostManager hosts,
        IWindowService window,
        IAppSettingsStore settings,
        IAccountsDialog accountsDialog,
        ISettingsDialog settingsDialog,
        IServiceRepository serviceRepository,
        FaviconCache favicons,
        UpdateService updates,
        LoginAssistService loginAssist,
        ILogger<MainViewModel> logger)
    {
        _catalog = catalog;
        _hosts = hosts;
        _window = window;
        _settings = settings;
        _accountsDialog = accountsDialog;
        _settingsDialog = settingsDialog;
        _serviceRepository = serviceRepository;
        _favicons = favicons;
        _updates = updates;
        _loginAssist = loginAssist;
        _logger = logger;

        _hosts.ActiveStateChanged += (_, _) => UpdateActiveState();
        _hosts.ServiceIconChanged += (_, host) =>
        {
            var item = Services.FirstOrDefault(s => s.Code == host.Service.Code);
            item?.Service.IconPath = host.Service.IconPath;
            item?.RefreshIcon();
        };
        _hosts.FullScreenElementChanged += OnFullScreenElementChanged;
        _hosts.HostFailed += OnHostFailed;
        _hosts.HostDisposed += (_, host) =>
        {
            var item = Services.FirstOrDefault(s => s.Code == host.Service.Code);
            if (item is not null)
            {
                item.IsLoaded = false;
            }
        };
        _loginAssist.Notice += OnLoginNotice;
        _window.FullScreenChanged += (_, _) => UpdateChromeVisibility();
        _window.MiniChanged += (_, _) =>
        {
            IsMini = _window.IsMini;
            UpdateChromeVisibility();
        };
    }

    private void UpdateChromeVisibility()
    {
        IsChromeVisible = !_window.IsFullScreen && !_window.IsMini;
        IsBannerOpen = IsBannerOpen && IsChromeVisible;
    }

    public ObservableCollection<ServiceItemViewModel> Services { get; } = [];

    [ObservableProperty]
    public partial ServiceItemViewModel? SelectedService { get; set; }

    /// <summary>전체화면이면 타이틀바·사이드바·배너를 숨긴다(FR-FS-02).</summary>
    [ObservableProperty]
    public partial bool IsChromeVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSidebarExpanded { get; set; }

    /// <summary>미니 플레이어 모드(FR-PLY-07). 위쪽의 얇은 드래그 바만 보인다.</summary>
    [ObservableProperty]
    public partial bool IsMini { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = "MoaView";

    [ObservableProperty]
    public partial bool IsBannerOpen { get; set; }

    [ObservableProperty]
    public partial string BannerTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BannerMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? BannerActionText { get; set; }

    [ObservableProperty]
    public partial string? BannerSecondaryText { get; set; }

    public async Task InitializeAsync()
    {
        _loginAssist.Start();

        var services = await _catalog.GetEnabledAsync();
        Services.Clear();
        foreach (var (service, index) in services.Select((s, i) => (s, i)))
        {
            Services.Add(new ServiceItemViewModel(service, index, this));
        }

        _logger.LogInformation("서비스 {Count}개 로드", Services.Count);

        IsSidebarExpanded = !await _settings.GetAsync(SettingKeys.SidebarCollapsed, true);

        // FR-SET-01 / FR-SVC-07: 고정 시작 서비스 또는 마지막 서비스. 없으면 첫 번째.
        var mode = await _settings.GetAsync(SettingKeys.StartupServiceMode, SettingKeys.StartupModeLast);
        var startId = mode == SettingKeys.StartupModeFixed
            ? await _settings.GetAsync<int?>(SettingKeys.StartupServiceId)
            : await _settings.GetAsync<int?>(SettingKeys.LastServiceId);
        var start = Services.FirstOrDefault(s => s.Service.Id == startId) ?? Services.FirstOrDefault();

        _isInitialized = true;
        if (start is not null)
        {
            await SelectServiceAsync(start);
        }

        _ = CheckForUpdatesInBackgroundAsync();
    }

    /// <summary>D-17: 설치된 앱이면 GitHub Releases에서 새 버전을 확인해 미리 받아 두고, 배너로 다시 시작을 권한다.</summary>
    private async Task CheckForUpdatesInBackgroundAsync()
    {
        if (!_updates.IsInstalled)
        {
            return;
        }

        try
        {
            await Task.Delay(UpdateCheckDelay);
            var version = await _updates.CheckAsync();
            if (version is null || !await _updates.DownloadAsync())
            {
                return;
            }

            ShowBanner(string.Format(Strings.UpdateReadyTitle, version), Strings.UpdateReadyMessage,
                Strings.Restart, () =>
                {
                    _updates.ApplyAndRestart();
                    return Task.CompletedTask;
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "백그라운드 업데이트 확인 실패");
        }
    }

    /// <summary>
    /// 설정 창이 닫힌 뒤 사이드바를 다시 읽는다. changedIds의 서비스(수정·삭제)와 비활성화된 서비스는 WebView를 해제해
    /// 다음 선택 때 새 설정으로 다시 만든다(마지막 URL은 DB에 있어 이어 본다). 선택은 가능하면 유지한다.
    /// </summary>
    public async Task ReloadServicesAsync(IReadOnlySet<int> changedIds)
    {
        var fresh = await _catalog.GetEnabledAsync();
        var selectedId = SelectedService?.Service.Id;

        foreach (var item in Services)
        {
            if (changedIds.Contains(item.Service.Id) || fresh.All(s => s.Id != item.Service.Id))
            {
                _hosts.RemoveHost(item.Code);
            }
        }

        Services.Clear();
        foreach (var (service, index) in fresh.Select((s, i) => (s, i)))
        {
            Services.Add(new ServiceItemViewModel(service, index, this) { IsLoaded = _hosts.GetHost(service.Code) is not null });
        }

        _logger.LogInformation("서비스 목록 다시 로드: {Count}개, 변경 {Changed}개", Services.Count, changedIds.Count);

        SelectedService = null;
        var next = Services.FirstOrDefault(s => s.Service.Id == selectedId) ?? Services.FirstOrDefault();
        if (next is not null)
        {
            await SelectServiceAsync(next);
        }
        else
        {
            UpdateActiveState();
        }
    }

    // ---------------------------------------------------------------- 서비스 선택·탐색

    [RelayCommand]
    private async Task SelectServiceAsync(ServiceItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        CloseBanner();
        await _hosts.ActivateAsync(item.Service);

        foreach (var service in Services)
        {
            service.IsSelected = service == item;
        }

        item.IsLoaded = true;
        SelectedService = item;
        UpdateActiveState();

        if (_isInitialized)
        {
            await _settings.SetAsync(SettingKeys.LastServiceId, item.Service.Id);
        }
    }

    partial void OnIsSidebarExpandedChanged(bool value)
    {
        if (_isInitialized)
        {
            _ = _settings.SetAsync(SettingKeys.SidebarCollapsed, !value);
        }
    }

    [RelayCommand]
    private Task SelectByIndexAsync(int index) =>
        index >= 0 && index < Services.Count ? SelectServiceAsync(Services[index]) : Task.CompletedTask;

    [RelayCommand]
    private Task NextServiceAsync() => StepAsync(+1);

    [RelayCommand]
    private Task PreviousServiceAsync() => StepAsync(-1);

    [RelayCommand]
    private void GoBack() => _hosts.GoBack();

    [RelayCommand]
    private void GoForward() => _hosts.GoForward();

    [RelayCommand]
    private void Reload() => _hosts.Reload();

    [RelayCommand]
    private void GoHome() => _hosts.GoHome();

    // ---------------------------------------------------------------- 컨텍스트 메뉴 (서비스 항목)

    [RelayCommand]
    private Task ManageAccountsAsync(ServiceItemViewModel? item) =>
        item is null ? Task.CompletedTask : _accountsDialog.ShowAsync(item.Service);

    /// <summary>컨텍스트 메뉴 "서비스 편집...": 설정 > 서비스 탭의 수정과 같다(FR-SVC-05).</summary>
    [RelayCommand]
    private async Task EditServiceAsync(ServiceItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var service = await _serviceRepository.GetByIdAsync(item.Service.Id);
        if (service is null || !ServiceEditDialog.Edit(service, isNew: false))
        {
            return;
        }

        if (!string.Equals(service.HomeUrl, item.Service.HomeUrl, StringComparison.OrdinalIgnoreCase))
        {
            // 다른 사이트로 바뀌었으면 캐시한 파비콘은 버리고 다시 받는다.
            _favicons.Delete(service.Code);
            service.IconPath = null;
        }

        await _serviceRepository.UpdateAsync(service);
        _logger.LogInformation("서비스 수정: {Code}", service.Code);
        await ReloadServicesAsync(new HashSet<int> { service.Id });
    }

    // ---------------------------------------------------------------- 설정·서비스 추가 (사이드바 하단)

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var result = await _settingsDialog.ShowAsync();
        if (result.ServicesChanged)
        {
            await ReloadServicesAsync(result.ChangedServiceIds);
        }
    }

    /// <summary>사이드바 ＋: 사용자 정의 서비스를 바로 추가하고 선택한다(FR-SVC-05).</summary>
    [RelayCommand]
    private async Task AddServiceAsync()
    {
        var all = await _serviceRepository.GetAllAsync();
        var draft = StreamingService.CreateCustom(all.Count + 1);
        if (!ServiceEditDialog.Edit(draft, isNew: true))
        {
            return;
        }

        await _serviceRepository.AddAsync(draft);
        _logger.LogInformation("서비스 추가: {Code} {Name}", draft.Code, draft.Name);
        await ReloadServicesAsync(new HashSet<int>());

        var added = Services.FirstOrDefault(s => s.Service.Id == draft.Id);
        if (added is not null)
        {
            await SelectServiceAsync(added);
        }
    }

    [RelayCommand]
    private void GoHomeOf(ServiceItemViewModel? item)
    {
        if (item is not null)
        {
            _hosts.GoHome(item.Service.Code);
        }
    }

    [RelayCommand]
    private void ReloadOf(ServiceItemViewModel? item)
    {
        if (item is not null)
        {
            _hosts.Reload(item.Service.Code);
        }
    }

    /// <summary>FR-ACC-06: 쿠키·사이트 데이터를 지워 로그아웃한다.</summary>
    [RelayCommand]
    private async Task LogoutAsync(ServiceItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (_hosts.GetHost(item.Service.Code) is null)
        {
            await SelectServiceAsync(item);
        }

        await _hosts.ClearSessionAsync(item.Service.Code);
        // 저장된 계정이 있으면 바로 로그인 페이지로 가서 자동 입력한다. 없으면 홈으로.
        await _loginAssist.AfterLogoutAsync(item.Service);
        ShowBanner(Strings.LoggedOutTitle, string.Format(Strings.LoggedOutMessage, item.Name), null, null);
    }

    /// <summary>개발용(Ctrl+Shift+L): 현재 페이지의 로그인 폼 구조를 로그에 남긴다. 입력 값은 포함되지 않는다.</summary>
    [RelayCommand]
    private async Task AnalyzeLoginFormAsync()
    {
        var host = _hosts.Active;
        if (host is null)
        {
            return;
        }

        var raw = await host.RunScriptForResultAsync(ScriptLoader.Load("form-analyze.js"));
        var json = ScriptLoader.ReadString(raw);
        if (json is null)
        {
            ShowBanner(Strings.FormAnalyzeFailedTitle, Strings.FormAnalyzeFailedMessage, null, null);
            return;
        }

        _logger.LogInformation("[{Service}] 로그인 폼 분석: {Json}", host.Service.Code, json);
        ShowBanner(Strings.FormAnalyzeDoneTitle,
            Strings.FormAnalyzeDoneMessage, null, null);
    }

    // ---------------------------------------------------------------- 전체화면·사이드바

    /// <summary>F11 / 사이드바 ⛶. 영상 전체화면 중이면 페이지 쪽에서 먼저 해제한다.</summary>
    [RelayCommand]
    private async Task ToggleFullScreenAsync()
    {
        switch (_window.Kind)
        {
            case FullScreenKind.Video:
                await _hosts.ExitVideoFullScreenAsync();
                break;
            case FullScreenKind.App:
                _window.ExitFullScreen();
                break;
            default:
                _window.EnterFullScreen(FullScreenKind.App);
                break;
        }
    }

    /// <summary>Esc: 전체화면·미니 플레이어 해제(FR-FS-03).</summary>
    [RelayCommand]
    private async Task EscapeAsync()
    {
        if (_window.Kind == FullScreenKind.Video)
        {
            await _hosts.ExitVideoFullScreenAsync();
        }
        else if (_window.Kind == FullScreenKind.App)
        {
            _window.ExitFullScreen();
        }
        else if (_window.IsMini)
        {
            _window.ExitMini();
        }
    }

    /// <summary>Ctrl+M: 미니 플레이어 토글(FR-PLY-07).</summary>
    [RelayCommand]
    private void ToggleMini()
    {
        if (_window.IsMini)
        {
            _window.ExitMini();
        }
        else
        {
            _window.EnterMini();
        }
    }

    /// <summary>Ctrl+P: PiP(FR-PLY-08).</summary>
    [RelayCommand]
    private Task TogglePictureInPictureAsync() => _hosts.TogglePictureInPictureAsync();

    [RelayCommand]
    private void ZoomIn() => _hosts.ZoomIn();

    [RelayCommand]
    private void ZoomOut() => _hosts.ZoomOut();

    [RelayCommand]
    private void ResetZoom() => _hosts.ResetZoom();

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    // ---------------------------------------------------------------- 배너

    [RelayCommand]
    private async Task BannerActionAsync()
    {
        var action = _bannerAction;
        CloseBanner();
        if (action is not null)
        {
            await action();
        }
    }

    [RelayCommand]
    private async Task BannerSecondaryActionAsync()
    {
        var action = _bannerSecondaryAction;
        CloseBanner();
        if (action is not null)
        {
            await action();
        }
    }

    [RelayCommand]
    private void CloseBanner()
    {
        IsBannerOpen = false;
        _bannerAction = null;
        _bannerSecondaryAction = null;
        BannerActionText = null;
        BannerSecondaryText = null;
    }

    private void ShowBanner(string title, string message, string? actionText, Func<Task>? action,
        string? secondaryText = null, Func<Task>? secondaryAction = null)
    {
        if (!IsChromeVisible)
        {
            // 전체화면 중에는 배너를 띄우지 않는다(06 UI/UX 3.2절). 로그로만 남긴다.
            _logger.LogInformation("배너 생략(전체화면): {Title}", title);
            return;
        }

        BannerTitle = title;
        BannerMessage = message;
        BannerActionText = actionText;
        BannerSecondaryText = secondaryText;
        _bannerAction = action;
        _bannerSecondaryAction = secondaryAction;
        IsBannerOpen = true;
    }

    private Task StepAsync(int delta)
    {
        if (Services.Count == 0)
        {
            return Task.CompletedTask;
        }

        var current = SelectedService?.Index ?? 0;
        var next = ((current + delta) % Services.Count + Services.Count) % Services.Count;
        return SelectServiceAsync(Services[next]);
    }

    private void UpdateActiveState()
    {
        var active = _hosts.Active;
        WindowTitle = active is null || string.IsNullOrWhiteSpace(active.DocumentTitle)
            ? "MoaView"
            : $"{active.DocumentTitle} - MoaView";
    }

    private void OnFullScreenElementChanged(object? sender, bool isFullScreen)
    {
        if (isFullScreen)
        {
            _window.EnterFullScreen(FullScreenKind.Video);
        }
        else if (_window.Kind == FullScreenKind.Video)
        {
            _window.ExitFullScreen();
        }
    }

    private void OnHostFailed(object? sender, HostFailedEventArgs e) =>
        ShowBanner(e.Title, e.Message, e.CanRetry ? Strings.Retry : null,
            e.CanRetry ? () => { _hosts.Reload(); return Task.CompletedTask; } : null);

    private void OnLoginNotice(object? sender, LoginNotice notice) =>
        ShowBanner(notice.Title, notice.Message, notice.ActionText, notice.Action, notice.SecondaryText, notice.SecondaryAction);
}
