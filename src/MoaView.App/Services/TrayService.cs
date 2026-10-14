using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using MoaView.App.Resources;
using MoaView.Core.Abstractions;
using MoaView.Core.Models;
using WinForms = System.Windows.Forms;

namespace MoaView.App.Services;

/// <summary>
/// FR-NAV-05 / D-18: 시스템 트레이 아이콘. 앱이 실행 중인 동안 항상 표시하고, 메뉴(열기·설정·종료)와 더블클릭 복원을 제공한다.
/// "닫으면 트레이로"(UI.CloseToTray)가 켜져 있으면 닫기(X)가 창을 숨기기만 한다. 숨긴 동안에도 재생은 계속된다.
/// WPF에는 NotifyIcon이 없어 WinForms 것을 쓴다. 미디어 키는 범위 밖(Could).
/// </summary>
public sealed class TrayService(IAppSettingsStore settings, ILogger<TrayService> logger) : IDisposable
{
    private WinForms.NotifyIcon? _icon;
    private Window? _window;
    private WindowState _stateBeforeHide = WindowState.Normal;
    private Action? _openSettings;

    /// <summary>닫기(X)를 트레이로 숨기기로 처리할지. 설정 창에서 바꾸면 바로 반영한다.</summary>
    public bool CloseToTray { get; set; }

    /// <summary>트레이 메뉴 "종료"나 다시 시작처럼 정말 끝내려는 경우 true. OnClosing이 보고 숨기지 않는다.</summary>
    public bool ExitRequested { get; private set; }

    public bool IsHidden => _window is { IsVisible: false };

    public async Task InitializeAsync(Window window, Action openSettings)
    {
        _window = window;
        _openSettings = openSettings;
        CloseToTray = await settings.GetAsync(SettingKeys.CloseToTray, false);

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(Strings.TrayOpen, null, (_, _) => ShowWindow());
        menu.Items.Add(Strings.Settings, null, (_, _) =>
        {
            ShowWindow();
            _openSettings?.Invoke();
        });
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Strings.TrayExit, null, (_, _) => Exit());

        _icon = new WinForms.NotifyIcon
        {
            Text = "MoaView",
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                ToggleWindow();
            }
        };
        logger.LogInformation("트레이 아이콘 표시. 닫으면 트레이로: {CloseToTray}", CloseToTray);
    }

    public Task SetCloseToTrayAsync(bool value)
    {
        CloseToTray = value;
        return settings.SetAsync(SettingKeys.CloseToTray, value);
    }

    /// <summary>창을 숨긴다(닫기 대신). 전체화면·미니는 호출자가 먼저 풀어야 한다.</summary>
    public void HideToTray()
    {
        if (_window is null)
        {
            return;
        }

        _stateBeforeHide = _window.WindowState == WindowState.Minimized ? WindowState.Normal : _window.WindowState;
        _window.Hide();
        logger.LogInformation("트레이로 숨김");
    }

    public void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        if (!_window.IsVisible)
        {
            _window.Show();
            _window.WindowState = _stateBeforeHide;
        }
        else if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    /// <summary>정말 종료한다. OnClosing은 ExitRequested를 보고 숨기지 않는다.</summary>
    public void Exit()
    {
        ExitRequested = true;
        _window?.Close();
    }

    private void ToggleWindow()
    {
        if (_window is null)
        {
            return;
        }

        if (_window.IsVisible && _window.WindowState != WindowState.Minimized && _window.IsActive)
        {
            HideToTray();
        }
        else
        {
            ShowWindow();
        }
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/moaview.ico"));
        using var stream = resource?.Stream ?? throw new FileNotFoundException("Assets/moaview.ico");
        return new System.Drawing.Icon(stream);
    }

    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
