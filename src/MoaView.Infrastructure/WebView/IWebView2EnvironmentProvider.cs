using Microsoft.Web.WebView2.Core;

namespace MoaView.Infrastructure.WebView;

/// <summary>
/// 앱 전체가 공유하는 CoreWebView2Environment 하나와 서비스별 프로필 옵션(04 아키텍처 4.3절).
/// </summary>
public interface IWebView2EnvironmentProvider
{
    /// <summary>설치된 Evergreen 런타임 버전. 없으면 null(설치 안내 필요).</summary>
    string? GetRuntimeVersion();

    Task<CoreWebView2Environment> GetEnvironmentAsync();

    /// <summary>프로필 이름(svc_{code})으로 쿠키를 격리하는 컨트롤러 옵션.</summary>
    Task<CoreWebView2ControllerOptions> CreateControllerOptionsAsync(string profileName);

    /// <summary>현재 환경이 띄운 브라우저·렌더러·GPU 프로세스 목록(메모리 진단용).</summary>
    IReadOnlyList<CoreWebView2ProcessInfo> GetProcessInfos();
}
