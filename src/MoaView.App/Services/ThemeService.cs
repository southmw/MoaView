using System.Windows;
using Microsoft.Extensions.Logging;
using MoaView.Core.Abstractions;
using MoaView.Core.Models;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MoaView.App.Services;

/// <summary>
/// FR-SET-02: 다크(기본)/라이트/시스템 테마. WPF-UI ApplicationThemeManager가 앱 리소스 사전을 바꾸므로 열린 창에 바로 반영된다.
/// "system"은 Windows 개인 설정의 앱 모드를 따르고, 메인 창을 SystemThemeWatcher로 감시해 실행 중 바뀌어도 따라간다.
/// </summary>
public sealed class ThemeService(IAppSettingsStore settings, ILogger<ThemeService> logger)
{
    private Window? _window;
    private bool _watching;

    /// <summary>SettingKeys.ThemeDark / ThemeLight / ThemeSystem.</summary>
    public string Current { get; private set; } = SettingKeys.ThemeDark;

    public async Task ApplySavedAsync() =>
        Apply(await settings.GetAsync(SettingKeys.Theme, SettingKeys.ThemeDark));

    public async Task SetAsync(string theme)
    {
        Apply(theme);
        await settings.SetAsync(SettingKeys.Theme, Current);
    }

    public void Apply(string theme)
    {
        Current = theme switch
        {
            SettingKeys.ThemeLight => SettingKeys.ThemeLight,
            SettingKeys.ThemeSystem => SettingKeys.ThemeSystem,
            _ => SettingKeys.ThemeDark,
        };

        var target = Current switch
        {
            SettingKeys.ThemeLight => ApplicationTheme.Light,
            SettingKeys.ThemeSystem => IsSystemDark() ? ApplicationTheme.Dark : ApplicationTheme.Light,
            _ => ApplicationTheme.Dark,
        };

        ApplicationThemeManager.Apply(target, WindowBackdropType.Mica, updateAccent: true);
        UpdateWatch();
        logger.LogInformation("테마 적용: {Theme} → {Applied}", Current, target);
    }

    /// <summary>메인 창이 로드된 뒤 호출한다. 시스템 모드일 때만 Windows 테마 변경을 감시한다.</summary>
    public void AttachWindow(Window window)
    {
        _window = window;
        UpdateWatch();
    }

    private void UpdateWatch()
    {
        if (_window is null)
        {
            return;
        }

        if (Current == SettingKeys.ThemeSystem)
        {
            if (!_watching)
            {
                SystemThemeWatcher.Watch(_window, WindowBackdropType.Mica, updateAccents: true);
                _watching = true;
            }
        }
        else if (_watching)
        {
            SystemThemeWatcher.UnWatch(_window);
            _watching = false;
        }
    }

    private static bool IsSystemDark() =>
        ApplicationThemeManager.GetSystemTheme() is SystemTheme.Dark or SystemTheme.Glow or SystemTheme.CapturedMotion or SystemTheme.HCBlack;
}
