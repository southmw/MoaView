namespace MoaView.Core.Models;

/// <summary>WebView2의 새 창 요청을 어떻게 처리할지(FR-NAV-04).</summary>
public enum NewWindowPolicy
{
    /// <summary>같은 WebView 안에서 연다(서비스 도메인 안의 링크).</summary>
    InApp,

    /// <summary>앱 안의 별도 대화상자 WebView로 연다(소셜 로그인 OAuth 팝업).</summary>
    OAuthDialog,

    /// <summary>기본 브라우저로 넘긴다(외부 도메인). 재생을 외부로 넘기는 용도가 아니다(D-07).</summary>
    ExternalBrowser,
}
