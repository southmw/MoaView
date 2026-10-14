using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MoaView.Core.Entities;
using Wpf.Ui.Controls;
using MoaView.App.Resources;

namespace MoaView.App.Views;

/// <summary>
/// 서비스 추가·수정(FR-SVC-05, 06 UI/UX 3절 서비스 탭). 저장을 누르면 넘겨받은 엔티티를 직접 갱신하고 true를 돌려준다.
/// 기본 제공 서비스는 코드·Provider를 바꿀 수 없고, 비워 둔 고급 항목은 Provider 기본값을 쓴다(NFR-08).
/// </summary>
public partial class ServiceEditDialog : FluentWindow
{
    private static readonly Regex ColorRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly Regex HostRegex = new(@"^(\*\.)?[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+$", RegexOptions.Compiled);

    private readonly StreamingService _service;

    private ServiceEditDialog(StreamingService service, bool isNew)
    {
        _service = service;
        InitializeComponent();

        Title = isNew ? Strings.AddService : string.Format(Strings.EditServiceTitle, service.Name);
        TitleBar.Title = Title;

        NameBox.Text = service.Name;
        HomeUrlBox.Text = service.HomeUrl;
        ColorBox.Text = service.AccentColor ?? string.Empty;
        LoginUrlBox.Text = service.LoginUrl ?? string.Empty;
        LoginPatternBox.Text = service.LoginUrlPattern ?? string.Empty;
        HostsBox.Text = string.Join(Environment.NewLine, service.AllowedHosts);

        if (service.IsBuiltIn)
        {
            NoteText.Text = string.Format(Strings.BuiltInServiceNote, service.Code);
            DefaultsHint.Text = Strings.BuiltInDefaultsHint;
        }
        else
        {
            DefaultsHint.Text = Strings.CustomDefaultsHint;
            AdvancedExpander.IsExpanded = !isNew && (service.LoginUrl is not null || service.AllowedHosts.Count > 0);
        }

        UpdateColorPreview();
    }

    /// <summary>저장했으면 true. 취소면 false이고 엔티티는 바뀌지 않는다.</summary>
    public static bool Edit(StreamingService service, bool isNew)
    {
        var dialog = new ServiceEditDialog(service, isNew)
        {
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow,
        };
        return dialog.ShowDialog() == true;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var homeUrl = HomeUrlBox.Text.Trim();
        var color = ColorBox.Text.Trim();
        var loginUrl = LoginUrlBox.Text.Trim();
        var pattern = LoginPatternBox.Text.Trim();
        var hosts = HostsBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (name.Length == 0)
        {
            ShowError(Strings.ServiceNameRequired);
            return;
        }

        if (!IsHttpUrl(homeUrl))
        {
            ShowError(Strings.HomeUrlInvalid);
            return;
        }

        if (color.Length > 0 && !ColorRegex.IsMatch(color))
        {
            ShowError(Strings.AccentColorInvalid);
            return;
        }

        if (loginUrl.Length > 0 && !IsHttpUrl(loginUrl))
        {
            ShowError(Strings.LoginUrlInvalid);
            return;
        }

        if (pattern.Length > 0)
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException ex)
            {
                ShowError(string.Format(Strings.LoginPatternInvalid, ex.Message));
                return;
            }
        }

        var badHost = hosts.FirstOrDefault(h => !HostRegex.IsMatch(h));
        if (badHost is not null)
        {
            ShowError(string.Format(Strings.AllowedHostInvalid, badHost));
            return;
        }

        _service.Name = name;
        _service.HomeUrl = homeUrl;
        _service.AccentColor = color.Length > 0 ? color.ToUpperInvariant() : null;
        _service.LoginUrl = loginUrl.Length > 0 ? loginUrl : null;
        _service.LoginUrlPattern = pattern.Length > 0 ? pattern : null;
        _service.AllowedHosts = hosts;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnColorChanged(object sender, TextChangedEventArgs e) => UpdateColorPreview();

    private void UpdateColorPreview()
    {
        var text = ColorBox.Text.Trim();
        if (ColorRegex.IsMatch(text))
        {
            ColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(text));
            return;
        }

        ColorPreview.Background = Brushes.Transparent;
    }

    private static bool IsHttpUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host.Length > 0;

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
