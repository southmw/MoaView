using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using MoaView.Core.Abstractions;
using MoaView.Core.Models;

namespace MoaView.App.Services;

/// <summary>
/// 04 아키텍처 4.4절. 창을 모니터 전체(작업 표시줄 포함)로 키우거나(전체화면) 작은 항상 위 창으로 줄이고(미니 플레이어) 되돌린다.
/// 셸 UI 숨김은 MainViewModel이 FullScreenChanged/MiniChanged를 받아 처리한다.
/// 미니 창의 위치·크기는 해제할 때 Window.MiniPlacement에 저장했다가 다음 진입 때 쓴다(FR-PLY-07).
/// </summary>
public sealed class WindowService(IAppSettingsStore settings, ILogger<WindowService> logger) : IWindowService
{
    private const double MiniWidth = 480;
    private const double MiniHeight = 270 + MiniBarHeight;
    private const double MiniBarHeight = 10;
    private const double MiniMargin = 16;

    /// <summary>미니 창의 최소 크기. 메인 창의 MinWidth/MinHeight(640×400)보다 작아야 480×280이 된다.</summary>
    private const double MiniMinWidth = 320;
    private const double MiniMinHeight = 180 + MiniBarHeight;

    private sealed record Placement(
        double Left, double Top, double Width, double Height,
        WindowState State, WindowStyle Style, ResizeMode Resize, bool Topmost,
        double MinWidth, double MinHeight);

    private Window? _window;
    private Placement? _saved;
    private WindowPlacement? _mini;
    private bool _miniDirty;

    public FullScreenKind Kind { get; private set; }

    public bool IsMini { get; private set; }

    public event EventHandler? FullScreenChanged;

    public event EventHandler? MiniChanged;

    public void Attach(Window window) => _window = window;

    /// <summary>저장된 미니 창 위치·크기를 읽는다. 메인 창 로드 뒤 한 번 호출한다.</summary>
    public async Task LoadAsync()
    {
        try
        {
            _mini = await settings.GetAsync<WindowPlacement>(SettingKeys.MiniPlacement);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "미니 창 위치 읽기 실패");
        }
    }

    /// <summary>미니 창 위치·크기가 바뀌었으면 저장한다. 종료 직전에는 동기로 기다린다.</summary>
    public async Task SaveMiniPlacementAsync()
    {
        if (!_miniDirty || _mini is null)
        {
            return;
        }

        _miniDirty = false;
        try
        {
            await settings.SetAsync(SettingKeys.MiniPlacement, _mini);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "미니 창 위치 저장 실패");
        }
    }

    // ---------------------------------------------------------------- 전체화면

    public void EnterFullScreen(FullScreenKind kind)
    {
        if (_window is null || kind == FullScreenKind.None)
        {
            return;
        }

        if (IsMini)
        {
            ExitMini();
        }

        if (Kind != FullScreenKind.None)
        {
            // 이미 전체화면이면 종류만 바꾼다(앱 전체화면 중 영상 전체화면 진입 등).
            Kind = kind;
            FullScreenChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _saved = Capture();

        var (monitor, _) = GetMonitorRects();
        var dpi = VisualTreeHelper.GetDpi(_window);

        _window.WindowState = WindowState.Normal;
        _window.WindowStyle = WindowStyle.None;
        _window.ResizeMode = ResizeMode.NoResize;
        _window.Topmost = true;
        _window.Left = monitor.Left / dpi.DpiScaleX;
        _window.Top = monitor.Top / dpi.DpiScaleY;
        _window.Width = monitor.Width / dpi.DpiScaleX;
        _window.Height = monitor.Height / dpi.DpiScaleY;

        Kind = kind;
        logger.LogInformation("전체화면 진입({Kind}): {Width}x{Height}px, DPI {Dpi:0.00}",
            kind, monitor.Width, monitor.Height, dpi.DpiScaleX);
        FullScreenChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ExitFullScreen()
    {
        if (_window is null || Kind == FullScreenKind.None)
        {
            return;
        }

        Restore();
        Kind = FullScreenKind.None;
        logger.LogInformation("전체화면 해제");
        FullScreenChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------------------------------------------------------------- 미니 플레이어

    public void EnterMini()
    {
        if (_window is null || IsMini)
        {
            return;
        }

        if (Kind != FullScreenKind.None)
        {
            ExitFullScreen();
        }

        _saved = Capture();

        _window.WindowState = WindowState.Normal;
        _window.WindowStyle = WindowStyle.None;
        _window.ResizeMode = ResizeMode.CanResizeWithGrip;
        _window.Topmost = true;
        _window.MinWidth = MiniMinWidth;
        _window.MinHeight = MiniMinHeight;

        if (_mini is { } saved && IsOnScreen(saved))
        {
            _window.Width = Math.Max(MiniMinWidth, saved.Width);
            _window.Height = Math.Max(MiniMinHeight, saved.Height);
            _window.Left = saved.X;
            _window.Top = saved.Y;
        }
        else
        {
            // 기본: 현재 모니터 작업 영역의 오른쪽 아래
            var (_, work) = GetMonitorRects();
            var dpi = VisualTreeHelper.GetDpi(_window);
            _window.Width = MiniWidth;
            _window.Height = MiniHeight;
            _window.Left = work.Right / dpi.DpiScaleX - MiniWidth - MiniMargin;
            _window.Top = work.Bottom / dpi.DpiScaleY - MiniHeight - MiniMargin;
        }

        IsMini = true;
        logger.LogInformation("미니 플레이어 진입 {Width:0}x{Height:0} @ {Left:0},{Top:0}", _window.Width, _window.Height, _window.Left, _window.Top);
        MiniChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ExitMini()
    {
        if (_window is null || !IsMini)
        {
            return;
        }

        var current = new WindowPlacement(_window.Left, _window.Top, _window.Width, _window.Height, nameof(WindowState.Normal));
        if (current != _mini)
        {
            _mini = current;
            _miniDirty = true;
        }

        Restore();
        IsMini = false;
        logger.LogInformation("미니 플레이어 해제");
        MiniChanged?.Invoke(this, EventArgs.Empty);
        _ = SaveMiniPlacementAsync();
    }

    // ---------------------------------------------------------------- 공통

    private Placement Capture() =>
        new(_window!.Left, _window.Top, _window.Width, _window.Height,
            _window.WindowState, _window.WindowStyle, _window.ResizeMode, _window.Topmost,
            _window.MinWidth, _window.MinHeight);

    private void Restore()
    {
        if (_saved is not { } p || _window is null)
        {
            return;
        }

        _window.Topmost = p.Topmost;
        _window.WindowStyle = p.Style;
        _window.ResizeMode = p.Resize;
        _window.MinWidth = p.MinWidth;
        _window.MinHeight = p.MinHeight;
        _window.WindowState = WindowState.Normal;
        _window.Left = p.Left;
        _window.Top = p.Top;
        _window.Width = p.Width;
        _window.Height = p.Height;
        _window.WindowState = p.State;
        _saved = null;
    }

    /// <summary>저장된 위치가 현재 가상 화면 안에 충분히 보이는지(모니터 구성이 바뀌었을 수 있다).</summary>
    private static bool IsOnScreen(WindowPlacement p)
    {
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;
        return p.Width >= MiniMinWidth && p.Height >= MiniMinHeight
               && p.X + 60 < right && p.Y + 40 < bottom
               && p.X + p.Width > left + 60 && p.Y >= top - 10;
    }

    private (NativeMethods.Rect Monitor, NativeMethods.Rect Work) GetMonitorRects() =>
        NativeMethods.GetMonitorRects(new WindowInteropHelper(_window!).Handle);
}
