using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using MoaView.App.Services;
using MoaView.App.ViewModels;
using MoaView.Core.Abstractions;
using MoaView.Core.Models;
using Wpf.Ui.Controls;

namespace MoaView.App.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly WindowService _windowService;
    private readonly ThemeService _theme;
    private readonly TrayService _tray;
    private readonly IAppSettingsStore _settings;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(
        MainViewModel viewModel,
        IServiceHostManager hostManager,
        WindowService windowService,
        ThemeService theme,
        TrayService tray,
        IAppSettingsStore settings,
        ILogger<MainWindow> logger)
    {
        _viewModel = viewModel;
        _windowService = windowService;
        _theme = theme;
        _tray = tray;
        _settings = settings;
        _logger = logger;

        InitializeComponent();
        DataContext = viewModel;

        windowService.Attach(this);
        hostManager.Attach(HostArea);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    /// <summary>FR-FS-05: 창 위치·크기·최대화 복원. 화면 밖이면 기본값을 쓴다.</summary>
    private async void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var placement = await _settings.GetAsync<WindowPlacement>(SettingKeys.WindowPlacement);
            if (placement is null)
            {
                return;
            }

            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
            var visible = placement.X + 100 < virtualRight && placement.Y + 100 < virtualBottom
                          && placement.X + placement.Width > virtualLeft + 100 && placement.Y > virtualTop - 50;
            if (visible && placement.Width >= MinWidth && placement.Height >= MinHeight)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = placement.X;
                Top = placement.Y;
                Width = placement.Width;
                Height = placement.Height;
            }

            if (placement.State == nameof(WindowState.Maximized))
            {
                WindowState = WindowState.Maximized;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "창 상태 복원 실패");
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            // 시스템 테마 모드일 때 Windows 테마 변경을 따라가려면 창 핸들이 필요해 Loaded 뒤에 붙인다.
            _theme.AttachWindow(this);
            await _windowService.LoadAsync();
            await _tray.InitializeAsync(this, () => _viewModel.OpenSettingsCommand.Execute(null));
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "초기화 실패");
        }
    }

    /// <summary>미니 플레이어의 얇은 바: 드래그로 이동, 더블클릭으로 복귀. WebView 위에는 올릴 수 없어 옆에 둔다(Airspace).</summary>
    private void OnMiniBarMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _viewModel.ToggleMiniCommand.Execute(null);
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 마우스 버튼이 이미 떼진 경우
            }
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_windowService.Kind != FullScreenKind.None)
        {
            _windowService.ExitFullScreen();
        }

        if (_windowService.IsMini)
        {
            _windowService.ExitMini();
        }

        // FR-NAV-05: 옵션이 켜져 있으면 닫기(X)는 트레이로 숨기기. 트레이 메뉴 "종료"·다시 시작은 ExitRequested로 구분한다.
        if (_tray.CloseToTray && !_tray.ExitRequested)
        {
            e.Cancel = true;
            _tray.HideToTray();
            return;
        }

        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        var placement = new WindowPlacement(bounds.X, bounds.Y, bounds.Width, bounds.Height, WindowState.ToString());
        try
        {
            // 종료 직전이라 동기로 기다린다. SQLite 로컬 쓰기라 수십 ms 안에 끝난다.
            _settings.SetAsync(SettingKeys.WindowPlacement, placement).GetAwaiter().GetResult();
            _windowService.SaveMiniPlacementAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "창 상태 저장 실패");
        }
    }

    /// <summary>
    /// 앱 단축키(06 UI/UX 4절). WebView2에 포커스가 있어도 WPF WebView2 컨트롤이 AcceleratorKeyPressed를
    /// PreviewKeyDown으로 올려 주므로 창에서 가로챌 수 있다. Space·화살표 등 플레이어 키는 건드리지 않는다.
    /// </summary>
    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var ctrl = modifiers.HasFlag(ModifierKeys.Control);
        var alt = modifiers.HasFlag(ModifierKeys.Alt);
        var shift = modifiers.HasFlag(ModifierKeys.Shift);

        switch (key)
        {
            case Key.F11 when modifiers == ModifierKeys.None:
                e.Handled = true;
                await _viewModel.ToggleFullScreenCommand.ExecuteAsync(null);
                break;

            case Key.Escape when modifiers == ModifierKeys.None:
                // 전체화면·미니가 아닐 때는 페이지에 넘긴다(플레이어 메뉴 닫기 등).
                if (_viewModel.IsChromeVisible)
                {
                    return;
                }

                e.Handled = true;
                await _viewModel.EscapeCommand.ExecuteAsync(null);
                break;

            case Key.F5 when modifiers == ModifierKeys.None:
            case Key.R when ctrl && !alt:
                e.Handled = true;
                _viewModel.ReloadCommand.Execute(null);
                break;

            case Key.Left when alt:
                e.Handled = true;
                _viewModel.GoBackCommand.Execute(null);
                break;

            case Key.Right when alt:
                e.Handled = true;
                _viewModel.GoForwardCommand.Execute(null);
                break;

            case Key.Home when alt:
                e.Handled = true;
                _viewModel.GoHomeCommand.Execute(null);
                break;

            case Key.B when ctrl && !alt:
                e.Handled = true;
                _viewModel.ToggleSidebarCommand.Execute(null);
                break;

            case Key.L when ctrl && shift && !alt:
                // 개발용: 로그인 폼 분석 결과를 로그에 기록
                e.Handled = true;
                await _viewModel.AnalyzeLoginFormCommand.ExecuteAsync(null);
                break;

            case Key.M when ctrl && !alt:
                e.Handled = true;
                _viewModel.ToggleMiniCommand.Execute(null);
                break;

            case Key.OemComma when ctrl && !alt:
                e.Handled = true;
                await _viewModel.OpenSettingsCommand.ExecuteAsync(null);
                break;

            case Key.P when ctrl && !alt:
                e.Handled = true;
                await _viewModel.TogglePictureInPictureCommand.ExecuteAsync(null);
                break;

            case Key.OemPlus or Key.Add when ctrl && !alt:
                e.Handled = true;
                _viewModel.ZoomInCommand.Execute(null);
                break;

            case Key.OemMinus or Key.Subtract when ctrl && !alt:
                e.Handled = true;
                _viewModel.ZoomOutCommand.Execute(null);
                break;

            case Key.D0 or Key.NumPad0 when ctrl && !alt:
                e.Handled = true;
                _viewModel.ResetZoomCommand.Execute(null);
                break;

            case Key.Tab when ctrl:
                e.Handled = true;
                if (shift)
                {
                    await _viewModel.PreviousServiceCommand.ExecuteAsync(null);
                }
                else
                {
                    await _viewModel.NextServiceCommand.ExecuteAsync(null);
                }
                break;

            case >= Key.D1 and <= Key.D9 when ctrl && !alt:
                e.Handled = true;
                await _viewModel.SelectByIndexCommand.ExecuteAsync(key - Key.D1);
                break;
        }
    }
}
