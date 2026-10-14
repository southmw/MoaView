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
using MoaView.App.Resources;

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
    private readonly TrayService _tray;
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
        TrayService tray,
        ILogger<SettingsViewModel> logger)
    {
        _favicons = favicons;
        _updates = updates;
        _tray = tray;
        UpdateStatus = _updates.IsInstalled
            ? (_updates.ReadyVersion is { } ready ? string.Format(Strings.UpdateDownloaded, ready) : Strings.UpdateCheckIdle)
            : Strings.UpdateNotInstalled;
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

    public IReadOnlyList<string> Tabs { get; } = [Strings.General, Strings.Service, Strings.Account, Strings.Playback, Strings.Shortcuts, Strings.Data, Strings.About];

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
        new(SettingKeys.ThemeDark, Strings.ThemeDark),
        new(SettingKeys.ThemeLight, Strings.ThemeLight),
        new(SettingKeys.ThemeSystem, Strings.ThemeSystem),
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
        new("F11", Strings.ShortcutFullScreen),
        new("Esc", Strings.ShortcutEscape),
        new("Ctrl + 1 ~ 9", Strings.ShortcutSelectService),
        new("Ctrl + Tab / Ctrl + Shift + Tab", Strings.ShortcutNextPrev),
        new("Ctrl + B", Strings.SidebarToggle),
        new("Alt + ← / Alt + →", Strings.ShortcutBackForward),
        new("F5 / Ctrl + R", Strings.Refresh),
        new("Alt + Home", Strings.ShortcutHome),
        new("Ctrl + + / Ctrl + - / Ctrl + 0", Strings.ShortcutZoom),
        new("Ctrl + M", Strings.ShortcutMini),
        new("Ctrl + P", Strings.ShortcutPip),
        new("Ctrl + ,", Strings.ShortcutSettings),
        new("Ctrl + Shift + L", Strings.ShortcutFormAnalyze),
    ];

    // ---------------------------------------------------------------- 데이터 (FR-SET-04, 05)

    [ObservableProperty]
    public partial string? DataStatus { get; set; }

    public string DataFolder => _paths.Root;

    // ---------------------------------------------------------------- 정보

    public string AppVersion => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "-";

    public string RuntimeVersion => _environment.GetRuntimeVersion() ?? Strings.NotInstalled;

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

        UpdateStatus = Strings.UpdateChecking;
        var version = await _updates.CheckAsync();
        if (version is null)
        {
            UpdateStatus = string.Format(Strings.UpdateLatest, _updates.CurrentVersion);
            return;
        }

        UpdateStatus = string.Format(Strings.UpdateDownloading, version);
        if (await _updates.DownloadAsync(p => UpdateStatus = string.Format(Strings.UpdateDownloadingProgress, version, p)))
        {
            UpdateStatus = string.Format(Strings.UpdateDownloaded, version);
            IsUpdateReady = true;
        }
        else
        {
            UpdateStatus = Strings.UpdateDownloadFailed;
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
        CloseToTray = _tray.CloseToTray;

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

    /// <summary>FR-NAV-05: 닫기(X)를 트레이로 숨기기로 처리.</summary>
    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (_loaded)
        {
            _ = _tray.SetCloseToTrayAsync(value);
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
        ServiceStatus = string.Format(Strings.ServiceAdded, draft.Name);
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
        ServiceStatus = string.Format(Strings.ServiceSaved, service.Name);
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
            Title = Strings.DeleteServiceTitle,
            Content = string.Format(Strings.DeleteServiceConfirm, row.Name),
            PrimaryButtonText = Strings.Delete,
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            CloseButtonText = Strings.Cancel,
        };
        if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            return;
        }

        await _services.DeleteAsync(row.Service.Id);
        _favicons.Delete(row.Service.Code);
        MarkChanged(row.Service.Id);
        _logger.LogInformation("서비스 삭제: {Code}", row.Service.Code);
        ServiceStatus = string.Format(Strings.ServiceDeleted, row.Name);
        await ReloadServicesAsync();
        await ReloadAccountsAsync();
    }

    private bool CanMoveUp => SelectedService is { } row && Services.IndexOf(row) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private Task MoveUpAsync() => MoveAsync(-1);

    private bool CanMoveDown => SelectedService is { } row && Services.IndexOf(row) < Services.Count - 1;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private Task MoveDownAsync() => MoveAsync(+1);

    private Task MoveAsync(int delta) =>
        SelectedService is { } row ? MoveToAsync(row, Services.IndexOf(row) + delta) : Task.CompletedTask;

    /// <summary>행을 targetIndex 위치로 옮기고 순서를 저장한다(FR-SVC-04). 드래그 정렬과 위/아래 버튼이 함께 쓴다.</summary>
    public async Task MoveToAsync(ServiceRowViewModel row, int targetIndex)
    {
        var index = Services.IndexOf(row);
        var target = Math.Clamp(targetIndex, 0, Services.Count - 1);
        if (index < 0 || target == index)
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
            Title = Strings.BackupDbTitle,
            FileName = $"moaview-backup-{DateTime.Now:yyyyMMdd-HHmm}.db",
            Filter = Strings.BackupDbFilter,
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
            DataStatus = string.Format(Strings.BackupDone, dialog.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DB 백업 실패: {Path}", dialog.FileName);
            DataStatus = string.Format(Strings.BackupFailed, ex.Message);
        }
    }

    /// <summary>FR-SET-04: 모든 서비스의 쿠키·캐시(WebView2 데이터 폴더)를 지운다. 실행 중에는 잠겨 있어 다음 시작 때 지운다.</summary>
    [RelayCommand]
    private async Task ResetWebViewDataAsync()
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = Strings.ResetAllTitle,
            Content = Strings.ResetAllConfirm,
            PrimaryButtonText = Strings.RestartNow,
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            SecondaryButtonText = Strings.NextLaunch,
            CloseButtonText = Strings.Cancel,
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

        DataStatus = Strings.ResetScheduled;
    }

    /// <summary>D-18: DB·브라우저 데이터·아이콘을 모두 지워 처음 상태로. 실행 중에는 잠겨 있어 표식 파일을 두고 다음 시작 때 지운다.</summary>
    [RelayCommand]
    private async Task ResetAllDataAsync()
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = Strings.ResetAllDataTitle,
            Content = Strings.ResetAllDataConfirm,
            PrimaryButtonText = Strings.RestartNow,
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            SecondaryButtonText = Strings.NextLaunch,
            CloseButtonText = Strings.Cancel,
        };
        var result = await box.ShowDialogAsync();
        if (result == Wpf.Ui.Controls.MessageBoxResult.None)
        {
            return;
        }

        DataReset.Schedule(_paths);
        _logger.LogWarning("모든 데이터 초기화 예약(다음 시작 때): {Marker}", _paths.ResetMarkerFile);
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            RestartRequested = true;
            Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault()?.Close();
            return;
        }

        DataStatus = Strings.ResetAllDataScheduled;
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
            DataStatus = string.Format(Strings.CannotOpenFolder, path);
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

    public string KindText => Service.IsBuiltIn ? Strings.Default : Strings.UserDefined;

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
                return Strings.NoSavedAccount;
            }

            var main = accounts.FirstOrDefault(a => a.IsDefault) ?? accounts[0];
            var mode = !main.AutoFillEnabled ? Strings.AutoLoginOff : main.AutoSubmitEnabled ? Strings.AutoFillSubmitLong : Strings.AutoFillOnlyLong;
            return accounts.Count == 1 ? $"{main.LoginId} · {mode}" : string.Format(Strings.AccountSummaryMany, main.LoginId, accounts.Count - 1, mode);
        }
    }
}
