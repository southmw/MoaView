using MoaView.Core.Models;

namespace MoaView.Core.Abstractions;

/// <summary>
/// 창 전체화면 제어(04 아키텍처 4.4절). 영상 전체화면과 앱 전체화면 두 진입 경로를 하나로 합친다.
/// 영상 전체화면의 해제는 페이지 쪽(exitFullscreen)에서 시작해야 하므로 호출자가 먼저 스크립트를 실행한다.
/// </summary>
public interface IWindowService
{
    FullScreenKind Kind { get; }

    bool IsFullScreen => Kind != FullScreenKind.None;

    void EnterFullScreen(FullScreenKind kind);

    void ExitFullScreen();

    event EventHandler? FullScreenChanged;

    /// <summary>미니 플레이어 모드(D-12, FR-PLY-07): 창 자체를 작은 항상 위 창으로 바꾼다. WebView는 그대로라 재생이 끊기지 않는다.</summary>
    bool IsMini { get; }

    void EnterMini();

    void ExitMini();

    event EventHandler? MiniChanged;
}
