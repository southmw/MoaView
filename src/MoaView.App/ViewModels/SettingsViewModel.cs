using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using MoaView.App.Services;
using MoaView.App.Views;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;
using MoaView.Core.Models;
using MoaView.Infrastructure.Configuration;
using MoaView.Infrastructure.WebView;

namespace MoaView.App.ViewModels;

/// <summary>
/// 설정 창(06 UI/UX 3절). 항목을 바꾸면 바로 저장한다(확인 버튼 없음).
/// 서비스 목록 변경은 ChangedServiceIds·ServicesChanged에 모아 두었다가 창이 닫힌 뒤 MainViewModel이 사이드바와 WebView에 반영한다.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public const int TabGeneral = 0;
    public const int TabServices = 1;
    public const int TabAccounts = 2;
    public const int TabPlayback = 3;
    public const int TabShortcuts = 4;
    public const int TabData = 5;
    public const int TabAbout = 6;

    private readonly IServiceRepository _services;
    private readonly IAccountRepository _accounts;
    private readonly IAccountsDialog _accountsDialog;
    private readonly IAppSettingsStore _settings;
    private readonly ThemeService _theme;
    private readonly IOptions<PlayerOptions> _playerOptions;
    private readonly IWebView2EnvironmentProvider _environment;
    private readonly AppPaths _paths;
    private readonly IDatabaseMaintenance _maintenance;
    private readonly FaviconCache _favicons;
    private readonly UpdateService _updates;
    private readonly ILogger<SettingsViewModel> _logger;

    private bool _loaded;

    public SettingsViewModel(
        IServiceRepository services,
        IAccountRepository accounts,
        IAccountsDialog accountsDialog,
        IAppSettingsStore settings,
        ThemeService theme,
        IOptions<PlayerOptions> playerOptions,
        IWebView2EnvironmentProvider environment,
        AppPaths paths,
        IDatabaseMaintenance maintenance,
        FaviconCache favicons,
        UpdateService updates,
        ILogger<SettingsViewModel> logger)
    {
        _favicons = favicons;
        _updates = updates;
        UpdateStatus = _updates.IsInstalled
            ? (_updates.ReadyVersion is { } ready ? $"v{ready} 다운로드 완료. 다시 시작하면 적용됩니다." : "GitHub Releases에서 새 버전을 확인합니다.")
            : "설치 버전이 아니라(개발 실행 또는 포터블) 자동 업데이트를 쓰지 않습니다.";
        IsUpdateReady = _updates.ReadyVersion is not null;
        _services = services;
        _accounts = accounts;
        _accountsDialog = accountsDialog;
        _settings = settings;
        _theme = theme;
        _playerOptions = playerOptions;
        _environment = environment;
        _paths = paths;
        _maintenance = maintenance;
        _logger = logger;
    }

    // ---------------------------------------------------------------- 창 닫힌 뒤 메인에 돌려줄 상태

    public HashSet<int> ChangedServiceIds { get; } = [];

    public bool ServicesChanged { get; private set; }

    /// <summary>WebView2 데이터 초기화처럼 다시 시작해야 적용되는 작업을 사용자가 지금 적용하기로 했다.</summary>
    public bool RestartRequested { get; private set; }

    public IReadOnlyList<string> Tabs { get; } = ["일반", "서비스", "계정", "재생", "단축키", "데이터", "정보"];

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    // ---------------------------------------------------------------- 일반 (FR-SET-01, FR-SET-02)

    public ObservableCollection<ServiceOption> StartupCandidates { get; } = [];

    [ObservableProperty]
    public partial bool StartWithLast { get; set; } = true;

    [ObservableProperty]
    public partial bool StartWithFixed { get; set; }

    [ObservableProperty]
    public partial ServiceOption? StartupService { get; set; }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(SettingKeys.ThemeDark, "다크 (기본)"),
        new(SettingKeys.ThemeLight, "라이트"),
        new(SettingKeys.ThemeSystem, "Windows 설정 따름"),
    ];

    [ObservableProperty]
    public partial ThemeOption? SelectedTheme { get; set; }

    // ---------------------------------------------------------------- 서비스 (FR-SVC-04, 05)

    public ObservableCollection<ServiceRowViewModel> Services { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditServiceCommand), nameof(DeleteServiceCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    public partial ServiceRowViewModel? SelectedService { get; set; }

    [ObservableProperty]
    public partial string? ServiceStatus { get; set; }

    // ---------------------------------------------------------------- 계정

    public ObservableCollection<AccountSummaryViewModel> AccountSummaries { get; } = [];

    // ---------------------------------------------------------------- 재생 (FR-PLY-05)

    [ObservableProperty]
    public partial double SuspendAfterSeconds { get; set; }

    [ObservableProperty]
    public partial double MaxAliveHosts { get; set; }

    // ---------------------------------------------------------------- 단축키 (06 UI/UX 4절)

    public IReadOnlyList<ShortcutRow> Shortcuts { get; } =
    [
        new("F11", "앱 전체화면 토글"),
        new("Esc", "전체화면·미니 플레이어 해제 (그 외에는 페이지로 전달)"),
        new("Ctrl + 1 ~ 9", "n번째 서비스로 전환"),
        new("Ctrl + Tab / Ctrl + Shift + Tab", "다음 / 이전 서비스"),
        new("Ctrl + B", "사이드바 펼치기/접기"),
        new("Alt + ← / Alt + →", "뒤로 / 앞으로 (마우스 측면 버튼도 동일)"),
        new("F5 / Ctrl + R", "새로고침"),
        new("Alt + Home", "서비스 홈"),
        new("Ctrl + + / Ctrl + - / Ctrl + 0", "확대 / 축소 / 배율 초기화 (서비스별 저장)"),
        new("Ctrl + M", "미니 플레이어 토글"),
        new("Ctrl + P", "PiP (화면 속 화면) 토글"),
        new("Ctrl + ,", "설정 열기"),
        new("Ctrl + Shift + L", "(개발용) 로그인 폼 구조를 로그에 기록"),
    ];

    // ---------------------------------------------------------------- 데이터 (FR-SET-04, 05)

    [ObservableProperty]
    public partial string? DataStatus { get; set; }

    public string DataFolder => _paths.Root;

    // ---------------------------------------------------------------- 정보

    public string AppVersion => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "-";

    public string RuntimeVersion => _environment.GetRuntimeVersion() ?? "설치되지 않음";

    public string DotNetVersion => Environment.Version.ToString();

    public string LogsFolder => _paths.LogsFolder;

    public string RepositoryUrl => UpdateService.RepositoryUrl;

    public bool CanCheckUpdates => _updates.IsInstalled;

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyUpdateCommand))]
    public partial bool IsUpdateReady { get; set; }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (!_updates.IsInstalled)
        {
            return;
        }

        UpdateStatus = "확인하는 중...";
        var version = await _updates.CheckAsync();
        if (version is null)
        {
            UpdateStatus = $"최신 버전입니다 (v{_updates.CurrentVersion}).";
            return;
        }

        UpdateStatus = $"v{version} 다운로드 중...";
        if (await _updates.DownloadAsync(p => UpdateStatus = $"v{version} 다운로드 중... {p}%"))
        {
            UpdateStatus = $"v{version} 다운로드 완료. 다시 시작하면 적용됩니다.";
            IsUpdateReady = true;
        }
        else
        {
            UpdateStatus = "다운로드에 실패했습니다. 로그를 확인하세요.";
        }
    }

    [RelayCommand(CanExecute = nameof(IsUpdateReady))]
    private void ApplyUpdate() => _updates.ApplyAndRestart();

    [RelayCommand]
    private void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo(UpdateService.RepositoryUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "저장소 페이지 열기 실패");
        }
    }

    // ---------------------------------------------------------------- 로드

    public async Task LoadAsync()
    {
        _loaded = false;

        var mode = await _settings.GetAsync(SettingKeys.StartupServiceMode, SettingKeys.StartupModeLast);
        StartWithFixed = mode == SettingKeys.StartupModeFixed;
        StartWithLast = !StartWithFixed;
        var fixedId = await _settings.GetAsync<int?>(SettingKeys.StartupServiceId);

        await ReloadServicesAsync();
        StartupService = StartupCandidates.FirstOrDefault(c => c.Id == fixedId) ?? StartupCandidates.FirstOrDefault();

        SelectedTheme = ThemeOptions.FirstOrDefault(t => t.Code == _theme.Current) ?? ThemeOptions[0];

        SuspendAfterSeconds = await _settings.GetAsync(SettingKeys.SuspendAfterSeconds, _playerOptions.Value.SuspendAfterSeconds);
        MaxAliveHosts = await _settings.GetAsync(SettingKeys.MaxAliveHosts, _playerOptions.Value.MaxAliveHosts);

        await ReloadAccountsAsync();

        _loaded = true;
    }

    private async Task ReloadServicesAsync(int? selectId = null)
    {
        var all = await _services.GetAllAsync();

        Services.Clear();
        foreach (var (service, index) in all.Select((s, i) => (s, i)))
        {
            Services.Add(new ServiceRowViewModel(service, index + 1, this));
        }

        SelectedService = Services.FirstOrDefault(s => s.Service.Id == selectId) ?? Services.FirstOrDefault();

        var keep = StartupService?.Id;
        StartupCandidates.Clear();
        foreach (var service in all.Where(s => s.IsEnabled))
        {
            StartupCandidates.Add(new ServiceOption(service.Id, service.Name));
        }

        StartupService = StartupCandidates.FirstOrDefault(c => c.Id == keep) ?? StartupCandidates.FirstOrDefault();
    }

    private async Task ReloadAccountsAsync()
    {
        AccountSummaries.Clear();
        foreach (var row in Services.Where(r => r.IsEnabled))
        {
            var accounts = await _accounts.GetByServiceAsync(row.Service.Id);
            AccountSummaries.Add(new AccountSummaryViewModel(row.Service, accounts));
        }
    }

    // ---------------------------------------------------------------- 일반: 변경 즉시 저장

    partial void OnStartWithLastChanged(bool value)
    {
        if (value)
        {
            StartWithFixed = false;
            _ = SaveStartupAsync();
        }
    }

    partial void OnStartWithFixedChanged(bool value)
    {
        if (value)
        {
            StartWithLast = false;
            _ = SaveStartupAsync();
        }
    }

    partial void OnStartupServiceChanged(ServiceOption? value)
    {
        if (StartWithFixed)
        {
            _ = SaveStartupAsync();
        }
    }

    private async Task SaveStartupAsync()
    {
        if (!_loaded)
        {
            return;
        }

        await _settings.SetAsync(SettingKeys.StartupServiceMode, StartWithFixed ? SettingKeys.StartupModeFixed : SettingKeys.StartupModeLast);
        if (StartupService is { } option)
        {
            await _settings.SetAsync(SettingKeys.StartupServiceId, option.Id);
        }
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (_loaded && value is not null)
        {
            _ = _theme.SetAsync(value.Code);
        }
    }

    // ---------------------------------------------------------------- 서비스

    [RelayCommand]
    private async Task AddServiceAsync()
    {
        var draft = StreamingService.CreateCustom(Services.Count + 1);
        if (!ServiceEditDialog.Edit(draft, isNew: true))
        {
            return;
        }

        await _services.AddAsync(draft);
        ServicesChanged = true;
        _logger.LogInformation("서비스 추가: {Code} {Name}", draft.Code, draft.Name);
        ServiceStatus = $"'{draft.Name}' 서비스를 추가했습니다.";
        await ReloadServicesAsync(draft.Id);
        await ReloadAccountsAsync();
    }

    private bool HasSelection => SelectedService is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditServiceAsync()
    {
        if (SelectedService is not { } row)
        {
            return;
        }

        var service = await _services.GetByIdAsync(row.Service.Id);
        if (service is null)
        {
            await ReloadServicesAsync();
            return;
        }

        var originalHomeUrl = service.HomeUrl;
        if (!ServiceEditDialog.Edit(service, isNew: false))
        {
            return;
        }

        if (!string.Equals(service.HomeUrl, originalHomeUrl, StringComparison.OrdinalIgnoreCase))
        {
            // 다른 사이트로 바뀌었으면 캐시한 파비콘은 버리고 다시 받는다.
            _favicons.Delete(service.Code);
            service.IconPath = null;
        }

        await _services.UpdateAsync(service);
        MarkChanged(service.Id);
        _logger.LogInformation("서비스 수정: {Code}", service.Code);
        ServiceStatus = $"'{service.Name}' 서비스를 저장했습니다. 열려 있던 화면은 다시 엽니다.";
        await ReloadServicesAsync(service.Id);
        await ReloadAccountsAsync();
    }

    private bool CanDelete => SelectedService is { IsBuiltIn: false };

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteServiceAsync()
    {
        if (SelectedService is not { IsBuiltIn: false } row)
        {
            return;
        }

        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "서비스 삭제",
            Content = $"'{row.Name}' 서비스와 저장된 계정을 모두 삭제할까요?\n이 서비스의 쿠키·사이트 데이터는 남아 있다가 다음 '모든 서비스 데이터 초기화' 때 지워집니다.",
            PrimaryButtonText = "삭제",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            CloseButtonText = "취소",
        };
        if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            return;
        }

        await _services.DeleteAsync(row.Service.Id);
        _favicons.Delete(row.Service.Code);
        MarkChanged(row.Service.Id);
        _logger.LogInformation("서비스 삭제: {Code}", row.Service.Code);
        ServiceStatus = $"'{row.Name}' 서비스를 삭제했습니다.";
        await ReloadServicesAsync();
        await ReloadAccountsAsync();
    }

    private bool CanMoveUp => SelectedService is { } row && Services.IndexOf(row) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private Task MoveUpAsync() => MoveAsync(-1);

    private bool CanMoveDown => SelectedService is { } row && Services.IndexOf(row) < Services.Count - 1;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private Task MoveDownAsync() => MoveAsync(+1);

    private async Task MoveAsync(int delta)
    {
        if (SelectedService is not { } row)
        {
            return;
        }

        var index = Services.IndexOf(row);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Services.Count)
        {
            return;
        }

        Services.Move(index, target);
        foreach (var (item, i) in Services.Select((s, i) => (s, i)))
        {
            item.Order = i + 1;
        }

        await _services.ReorderAsync(Services.Select(s => s.Service.Id).ToList());
        ServicesChanged = true;
        SelectedService = row;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    /// <summary>행의 체크박스가 바뀌었을 때(활성화·전환 시 일시정지). ServiceRowViewModel이 호출한다.</summary>
    internal async Task SaveRowAsync(ServiceRowViewModel row, bool enabledChanged)
    {
        if (!_loaded)
        {
            return;
        }

        if (enabledChanged)
        {
            await _services.UpdateEnabledAsync(row.Service.Id, row.IsEnabled);
            ServicesChanged = true;
            await ReloadAccountsAsync();
            return;
        }

        var service = await _services.GetByIdAsync(row.Service.Id);
        if (service is null)
        {
            return;
        }

        service.PauseOnSwitch = row.PauseOnSwitch;
        await _services.UpdateAsync(service);
        MarkChanged(service.Id);
    }

    private void MarkChanged(int serviceId)
    {
        ChangedServiceIds.Add(serviceId);
        ServicesChanged = true;
    }

    // ---------------------------------------------------------------- 계정

    [RelayCommand]
    private async Task ManageAccountsAsync(AccountSummaryViewModel? summary)
    {
        if (summary is null)
        {
            return;
        }

        await _accountsDialog.ShowAsync(summary.Service);
        await ReloadAccountsAsync();
    }

    // ---------------------------------------------------------------- 재생

    partial void OnSuspendAfterSecondsChanged(double value)
    {
        if (_loaded)
        {
            _ = _settings.SetAsync(SettingKeys.SuspendAfterSeconds, (int)Math.Clamp(value, 0, 86400));
        }
    }

    partial void OnMaxAliveHostsChanged(double value)
    {
        if (_loaded)
        {
            _ = _settings.SetAsync(SettingKeys.MaxAliveHosts, (int)Math.Clamp(value, 1, 10));
        }
    }

    // ---------------------------------------------------------------- 데이터

    [RelayCommand]
    private void OpenDataFolder() => OpenFolder(_paths.Root);

    [RelayCommand]
    private void OpenLogsFolder() => OpenFolder(_paths.LogsFolder);

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "DB 백업",
            FileName = $"moaview-backup-{DateTime.Now:yyyyMMdd-HHmm}.db",
            Filter = "SQLite 데이터베이스 (*.db)|*.db|모든 파일 (*.*)|*.*",
            DefaultExt = ".db",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _maintenance.BackupAsync(dialog.FileName);
            _logger.LogInformation("DB 백업: {Path}", dialog.FileName);
            DataStatus = $"백업했습니다: {dialog.FileName}\n저장된 비밀번호는 같은 Windows 계정·같은 설치에서만 복원됩니다.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DB 백업 실패: {Path}", dialog.FileName);
            DataStatus = $"백업에 실패했습니다: {ex.Message}";
        }
    }

    /// <summary>FR-SET-04: 모든 서비스의 쿠키·캐시(WebView2 데이터 폴더)를 지운다. 실행 중에는 잠겨 있어 다음 시작 때 지운다.</summary>
    [RelayCommand]
    private async Task ResetWebViewDataAsync()
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "모든 서비스 데이터 초기화",
            Content = "모든 서비스의 로그인 세션(쿠키)과 캐시를 지웁니다. 저장된 계정과 설정은 남습니다.\n브라우저 데이터는 실행 중에 지울 수 없어 앱을 다시 시작할 때 지워집니다. 지금 다시 시작할까요?",
            PrimaryButtonText = "지금 다시 시작",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            SecondaryButtonText = "다음 실행 때",
            CloseButtonText = "취소",
        };
        var result = await box.ShowDialogAsync();
        if (result == Wpf.Ui.Controls.MessageBoxResult.None)
        {
            return;
        }

        await _settings.SetAsync(SettingKeys.ResetWebViewOnStart, true);
        _logger.LogInformation("WebView2 데이터 초기화 예약(다음 시작 때)");
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            RestartRequested = true;
            Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault()?.Close();
            return;
        }

        DataStatus = "다음에 앱을 시작할 때 모든 서비스의 쿠키·캐시를 지웁니다.";
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "폴더 열기 실패: {Path}", path);
            DataStatus = $"폴더를 열 수 없습니다: {path}";
        }
    }
}

public sealed record ServiceOption(int Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record ThemeOption(string Code, string Name)
{
    public override string ToString() => Name;
}

public sealed record ShortcutRow(string Keys, string Action);

/// <summary>서비스 탭의 행. 체크박스 변경은 바로 저장한다.</summary>
public sealed partial class ServiceRowViewModel : ObservableObject
{
    private readonly SettingsViewModel _owner;
    private readonly bool _initialized;

    public ServiceRowViewModel(StreamingService service, int order, SettingsViewModel owner)
    {
        Service = service;
        _owner = owner;
        Order = order;
        // 초기값을 넣는 동안은 저장 콜백이 돌면 안 된다(모든 행이 '변경됨'으로 잡혀 WebView가 전부 다시 만들어진다).
        IsEnabled = service.IsEnabled;
        PauseOnSwitch = service.PauseOnSwitch;
        _initialized = true;
    }

    public StreamingService Service { get; }

    [ObservableProperty]
    public partial int Order { get; set; }

    public string Name => Service.Name;

    public string Host => Uri.TryCreate(Service.HomeUrl, UriKind.Absolute, out var uri) ? uri.Host : Service.HomeUrl;

    public bool IsBuiltIn => Service.IsBuiltIn;

    public string KindText => Service.IsBuiltIn ? "기본" : "사용자";

    public string ZoomText => $"{Service.ZoomFactor * 100:0}%";

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    public partial bool PauseOnSwitch { get; set; }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_initialized)
        {
            _ = _owner.SaveRowAsync(this, enabledChanged: true);
        }
    }

    partial void OnPauseOnSwitchChanged(bool value)
    {
        if (_initialized)
        {
            _ = _owner.SaveRowAsync(this, enabledChanged: false);
        }
    }
}

/// <summary>계정 탭의 행: 서비스별 계정 요약. 편집은 기존 AccountsWindow를 연다.</summary>
public sealed class AccountSummaryViewModel(StreamingService service, IReadOnlyList<Account> accounts)
{
    public StreamingService Service { get; } = service;

    public string Name => Service.Name;

    public string Summary
    {
        get
        {
            if (accounts.Count == 0)
            {
                return "저장된 계정 없음 (세션만 유지)";
            }

            var main = accounts.FirstOrDefault(a => a.IsDefault) ?? accounts[0];
            var mode = !main.AutoFillEnabled ? "자동 로그인 꺼짐" : main.AutoSubmitEnabled ? "자동 입력 + 제출" : "자동 입력만";
            return accounts.Count == 1 ? $"{main.LoginId} · {mode}" : $"{main.LoginId} 외 {accounts.Count - 1}개 · {mode}";
        }
    }
}
