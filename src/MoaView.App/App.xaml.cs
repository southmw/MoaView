using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoaView.App.Services;
using MoaView.App.ViewModels;
using MoaView.App.Views;
using MoaView.Core.Abstractions;
using MoaView.Core.Models;
using MoaView.Infrastructure.Configuration;
using MoaView.Infrastructure.DependencyInjection;
using MoaView.Infrastructure.Diagnostics;
using MoaView.Infrastructure.WebView;
using Serilog;
using MoaView.App.Resources;

namespace MoaView.App;

public partial class App : Application
{
    /// <summary>처리된 UI 예외를 사용자에게 알리는 횟수 상한. 같은 오류가 반복되면 로그만 남긴다.</summary>
    private const int MaxErrorDialogsPerSession = 3;

    private IHost? _host;
    private SingleInstanceGuard? _singleInstance;
    private int _errorDialogsShown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 07 개발 계획 Phase 4: 중복 실행 방지. 이미 떠 있으면 그 창을 앞으로 가져오게 하고 조용히 끝낸다.
        _singleInstance = new SingleInstanceGuard();
        if (!_singleInstance.IsFirstInstance)
        {
            _singleInstance.SignalExistingInstance();
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        var paths = new AppPaths();
        paths.EnsureCreated();

        var builder = Host.CreateApplicationBuilder(e.Args);

        // NFR-09: 파일 로그, 일 단위 롤링, 7일 보관. 비밀번호는 절대 로그에 남기지 않는다(NFR-05).
        builder.Services.AddSerilog((_, configuration) => configuration
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(paths.LogsFolder, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

        builder.Services.Configure<PlayerOptions>(builder.Configuration.GetSection(PlayerOptions.SectionName));
        builder.Services.Configure<LoginOptions>(builder.Configuration.GetSection(LoginOptions.SectionName));
        builder.Services.AddInfrastructure(paths);

        builder.Services.AddSingleton<WindowService>();
        builder.Services.AddSingleton<IWindowService>(sp => sp.GetRequiredService<WindowService>());
        builder.Services.AddSingleton<ServiceHostFactory>();
        builder.Services.AddSingleton<FaviconCache>();
        builder.Services.AddSingleton<IServiceHostManager, ServiceHostManager>();
        builder.Services.AddSingleton<IAccountsDialog, AccountsDialogService>();
        builder.Services.AddSingleton<ISettingsDialog, SettingsDialogService>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<UpdateService>();
        builder.Services.AddSingleton<LoginAssistService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        var environmentProvider = _host.Services.GetRequiredService<IWebView2EnvironmentProvider>();
        var appVersion = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        RegisterExceptionHandlers(paths, logger, appVersion, environmentProvider);

        logger.LogInformation("MoaView {Version} 시작. 데이터 폴더 {Root}", appVersion, paths.Root);
        logger.LogDebug("Debug 로깅 활성 (Debug 빌드)");

        _singleInstance.ListenForActivation(() => Dispatcher.InvokeAsync(ActivateMainWindow));

        // 06 UI/UX 5절: WebView2 런타임이 없으면 부트스트래퍼로 설치를 시도하고, 안 되면 다운로드 페이지를 안내한 뒤 종료한다.
        if (environmentProvider.GetRuntimeVersion() is null && !await EnsureWebView2RuntimeAsync(logger, environmentProvider))
        {
            Shutdown();
            return;
        }

        await _host.StartAsync();

        // 04 아키텍처 5.1절: 마이그레이션 적용 → 기본 서비스 Seed → 창 표시.
        try
        {
            await _host.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "DB 초기화 실패: {Path}", paths.DatabaseFile);
            MessageBox.Show(string.Format(Strings.DbOpenFailed, paths.DatabaseFile, ex.Message),
                "MoaView", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // FR-SET-04: 설정 > 데이터에서 예약한 전체 초기화는 WebView2 환경을 만들기 전에 폴더째 지운다.
        await ResetWebViewDataIfRequestedAsync(_host.Services.GetRequiredService<IAppSettingsStore>(), paths, logger);

        // FR-SET-02: 창을 만들기 전에 저장된 테마를 적용해 첫 화면부터 맞춘다.
        await _host.Services.GetRequiredService<ThemeService>().ApplySavedAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    /// <summary>Phase 4 WebView2 부트스트래퍼. 설치 뒤 런타임이 잡히면 true, 거절·실패면 다운로드 페이지를 안내하고 false.</summary>
    private static async Task<bool> EnsureWebView2RuntimeAsync(ILogger<App> logger, IWebView2EnvironmentProvider environmentProvider)
    {
        logger.LogError("WebView2 런타임이 설치되어 있지 않습니다.");
        var install = MessageBox.Show(
            Strings.WebView2InstallPrompt,
            "MoaView", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (install == MessageBoxResult.Yes)
        {
            var ok = await WebView2Bootstrapper.TryInstallAsync(logger);
            if (ok && environmentProvider.GetRuntimeVersion() is { } version)
            {
                logger.LogInformation("WebView2 런타임 설치 완료: {Version}", version);
                return true;
            }
        }

        var open = MessageBox.Show(
            Strings.WebView2InstallFailed,
            "MoaView", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (open == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo(WebView2Bootstrapper.DownloadPageUrl) { UseShellExecute = true });
        }

        return false;
    }

    /// <summary>
    /// Phase 4 전역 예외 처리. UI 예외는 로그 + 안내(세션당 최대 3회) 후 계속 실행하고, 복구 불가능한 예외는 크래시 로그를 남기고 안내한다.
    /// 비동기 작업에서 관찰되지 않은 예외는 로그만 남긴다. 예외 메시지에는 비밀번호를 넣지 않는 것이 원칙이다(NFR-05).
    /// </summary>
    private void RegisterExceptionHandlers(AppPaths paths, ILogger<App> logger, string appVersion, IWebView2EnvironmentProvider environmentProvider)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError(args.Exception, "처리되지 않은 UI 예외");
            args.Handled = true;

            if (_errorDialogsShown >= MaxErrorDialogsPerSession)
            {
                return;
            }

            _errorDialogsShown++;
            var suffix = _errorDialogsShown == MaxErrorDialogsPerSession ? Strings.ErrorDialogSuffix : string.Empty;
            MessageBox.Show(
                string.Format(Strings.UnhandledErrorMessage, args.Exception.GetType().Name, args.Exception.Message, paths.LogsFolder, suffix),
                "MoaView", MessageBoxButton.OK, MessageBoxImage.Warning);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception ?? new InvalidOperationException($"알 수 없는 예외 객체: {args.ExceptionObject}");
            logger.LogCritical(exception, "치명적 오류 (종료 {Terminating})", args.IsTerminating);
            var report = CrashReportWriter.Write(paths, exception, "AppDomain", appVersion, SafeRuntimeVersion(environmentProvider));
            Log.CloseAndFlush();

            if (args.IsTerminating)
            {
                MessageBox.Show(
                    string.Format(Strings.FatalErrorMessage, exception.GetType().Name, exception.Message, report ?? paths.LogsFolder),
                    "MoaView", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogWarning(args.Exception, "관찰되지 않은 비동기 예외");
            args.SetObserved();
        };
    }

    private static string? SafeRuntimeVersion(IWebView2EnvironmentProvider environmentProvider)
    {
        try
        {
            return environmentProvider.GetRuntimeVersion();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>두 번째 인스턴스가 신호를 보내면 창을 앞으로 가져온다. 미니 플레이어(Topmost)는 그 상태를 유지한다.</summary>
    private void ActivateMainWindow()
    {
        if (MainWindow is not { } window)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        var topmost = window.Topmost;
        window.Topmost = true;
        window.Topmost = topmost;
        window.Activate();
        window.Focus();
    }

    /// <summary>
    /// 이전 인스턴스가 아직 종료 중이면 브라우저 프로세스가 폴더를 잡고 있을 수 있어 잠시 재시도한다.
    /// 끝내 실패하면 플래그를 남겨 다음 시작 때 다시 시도한다.
    /// </summary>
    private static async Task ResetWebViewDataIfRequestedAsync(IAppSettingsStore settings, AppPaths paths, ILogger<App> logger)
    {
        if (!await settings.GetAsync(SettingKeys.ResetWebViewOnStart, false))
        {
            return;
        }

        const int attempts = 10;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                if (Directory.Exists(paths.WebView2UserDataFolder))
                {
                    Directory.Delete(paths.WebView2UserDataFolder, recursive: true);
                }

                await settings.RemoveAsync(SettingKeys.ResetWebViewOnStart);
                logger.LogInformation("WebView2 데이터 폴더 초기화 완료(사용자 요청): {Folder}", paths.WebView2UserDataFolder);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == attempts)
                {
                    logger.LogWarning(ex, "WebView2 데이터 폴더를 지우지 못했습니다. 다음 시작 때 다시 시도합니다.");
                    return;
                }

                await Task.Delay(500);
            }
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }
}
