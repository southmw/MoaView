namespace MoaView.Core.Models;

/// <summary>전체화면 진입 경로. 둘 다 창은 모니터 전체를 덮지만 해제 방법이 다르다(04 아키텍처 4.4절).</summary>
public enum FullScreenKind
{
    None,

    /// <summary>F11. 셸 UI만 숨기고 웹페이지는 그대로다.</summary>
    App,

    /// <summary>웹 플레이어의 전체화면 버튼(HTML5 Fullscreen API). 해제는 페이지 쪽 exitFullscreen()으로 한다.</summary>
    Video,
}
