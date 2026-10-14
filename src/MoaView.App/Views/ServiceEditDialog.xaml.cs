using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MoaView.Core.Entities;
using Wpf.Ui.Controls;

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

        Title = isNew ? "서비스 추가" : $"서비스 수정 - {service.Name}";
        TitleBar.Title = Title;

        NameBox.Text = service.Name;
        HomeUrlBox.Text = service.HomeUrl;
        ColorBox.Text = service.AccentColor ?? string.Empty;
        LoginUrlBox.Text = service.LoginUrl ?? string.Empty;
        LoginPatternBox.Text = service.LoginUrlPattern ?? string.Empty;
        HostsBox.Text = string.Join(Environment.NewLine, service.AllowedHosts);

        if (service.IsBuiltIn)
        {
            NoteText.Text = $"기본 제공 서비스({service.Code})는 삭제할 수 없고 숨길 수만 있습니다. 고급 항목을 비워 두면 앱에 내장된 기본값을 씁니다.";
            DefaultsHint.Text = "비워 두면 이 서비스 전용 Provider의 기본 로그인 URL·패턴·허용 호스트를 씁니다.";
        }
        else
        {
            DefaultsHint.Text = "비워 두면 홈 URL의 호스트와 하위 도메인만 앱 안에서 열고, 그 밖의 링크는 기본 브라우저로 넘깁니다.";
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
            ShowError("이름을 입력하세요.");
            return;
        }

        if (!IsHttpUrl(homeUrl))
        {
            ShowError("홈 URL은 http:// 또는 https://로 시작하는 전체 주소여야 합니다.");
            return;
        }

        if (color.Length > 0 && !ColorRegex.IsMatch(color))
        {
            ShowError("강조 색은 #RRGGBB 형식이어야 합니다(예: #3B82F6).");
            return;
        }

        if (loginUrl.Length > 0 && !IsHttpUrl(loginUrl))
        {
            ShowError("로그인 페이지 URL은 http:// 또는 https://로 시작하는 전체 주소여야 합니다.");
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
                ShowError($"로그인 URL 정규식이 잘못되었습니다: {ex.Message}");
                return;
            }
        }

        var badHost = hosts.FirstOrDefault(h => !HostRegex.IsMatch(h));
        if (badHost is not null)
        {
            ShowError($"허용 호스트 형식이 잘못되었습니다: {badHost} (예: *.example.com 또는 www.example.com)");
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
