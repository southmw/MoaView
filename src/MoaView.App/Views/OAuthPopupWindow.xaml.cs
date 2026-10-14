using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Wpf.Ui.Controls;
using MoaView.App.Resources;

namespace MoaView.App.Views;

/// <summary>
/// 소셜 로그인(OAuth) 팝업을 앱 안에서 여는 창(FR-NAV-04). 요청한 서비스와 같은 프로필을 써서 로그인 쿠키가 공유된다.
/// 페이지가 window.close()를 부르면 창을 닫는다. WebView2 보안 설정은 본 창(ServiceHostControl)과 같게 둔다(10 보안 점검 S-13).
/// </summary>
public partial class OAuthPopupWindow : FluentWindow
{
    public OAuthPopupWindow()
    {
        InitializeComponent();
    }

    public async Task<CoreWebView2> InitializeAsync(CoreWebView2Environment environment, CoreWebView2ControllerOptions options)
    {
        await PopupView.EnsureCoreWebView2Async(environment, options);
        var core = PopupView.CoreWebView2;

        var settings = core.Settings;
        settings.IsStatusBarEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
#if !DEBUG
        settings.AreDevToolsEnabled = false;
#endif

        // 권한은 자동 재생만, 외부 프로토콜은 차단, 팝업 안의 새 창은 http(s)만 기본 브라우저로 넘긴다.
        core.PermissionRequested += (_, e) =>
        {
            e.State = e.PermissionKind == CoreWebView2PermissionKind.Autoplay
                ? CoreWebView2PermissionState.Allow
                : CoreWebView2PermissionState.Deny;
            e.Handled = true;
        };
        core.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) && target.Scheme is "https" or "http")
            {
                try
                {
                    Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true });
                }
                catch
                {
                    // 기본 브라우저 실행 실패는 무시한다(팝업 안에서 처리할 수단이 없다).
                }
            }
        };

        core.DocumentTitleChanged += (_, _) =>
        {
            Title = string.IsNullOrWhiteSpace(core.DocumentTitle) ? Strings.Login : core.DocumentTitle;
            TitleBar.Title = Title;
        };
        core.WindowCloseRequested += (_, _) => Close();

        Closed += (_, _) => PopupView.Dispose();
        return core;
    }
}
