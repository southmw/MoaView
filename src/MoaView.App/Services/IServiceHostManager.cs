using System.Windows.Controls;
using MoaView.App.Controls;
using MoaView.Core.Entities;

namespace MoaView.App.Services;

/// <summary>
/// 서비스별 ServiceHostControl의 수명을 관리한다(04 아키텍처 5.2절 서비스 전환).
/// 처음 선택할 때 생성(Lazy), 전환은 Visibility만 바꾼다. 생성은 보이는 상태에서 해야 한다(R-20: 숨긴 채 로드하면 40초 지연).
/// 비활성 호스트는 일정 시간 뒤 Suspend하고(FR-PLY-05), 최대 유지 개수를 넘으면 가장 오래 안 쓴 호스트를 해제한다(LRU).
/// </summary>
public interface IServiceHostManager
{
    /// <summary>MainWindow가 WebView를 올릴 패널을 넘긴다.</summary>
    void Attach(Panel hostArea);

    ServiceHostControl? Active { get; }

    ServiceHostControl? GetHost(string serviceCode);

    Task ActivateAsync(StreamingService service);

    /// <summary>서비스가 수정·비활성화·삭제됐을 때 호스트를 해제한다(설정 창 반영). 활성 호스트였으면 Active는 null이 된다.</summary>
    void RemoveHost(string serviceCode);

    void GoBack();
    void GoForward();
    void Reload();
    void GoHome();

    /// <summary>특정 서비스(활성이 아니어도)의 홈으로 간다. 아직 생성되지 않았으면 아무것도 하지 않는다.</summary>
    void GoHome(string serviceCode);

    void Reload(string serviceCode);

    /// <summary>FR-ACC-06: 서비스 프로필의 쿠키·사이트 데이터를 지운다(로그아웃).</summary>
    Task ClearSessionAsync(string serviceCode);

    /// <summary>영상 전체화면을 페이지 쪽(exitFullscreen)에서 해제한다.</summary>
    Task ExitVideoFullScreenAsync();

    /// <summary>FR-PLY-08: 활성 서비스의 영상을 PiP로 띄우거나 닫는다.</summary>
    Task TogglePictureInPictureAsync();

    void ZoomIn();
    void ZoomOut();
    void ResetZoom();

    /// <summary>호스트가 새로 만들어졌다(초기화 전). 로그인 보조 등이 이벤트를 구독할 때 쓴다.</summary>
    event EventHandler<ServiceHostControl>? HostCreated;

    /// <summary>LRU로 호스트가 해제됐다(사이드바의 로드 표시를 지우기 위해).</summary>
    event EventHandler<ServiceHostControl>? HostDisposed;

    /// <summary>활성 서비스의 웹 플레이어가 전체화면 요소에 들어가거나 나왔다.</summary>
    event EventHandler<bool>? FullScreenElementChanged;

    /// <summary>활성 서비스의 URL·제목이 바뀌었다.</summary>
    event EventHandler? ActiveStateChanged;

    /// <summary>서비스의 파비콘을 새로 캐시해 Service.IconPath가 바뀌었다(사이드바 아이콘 갱신용).</summary>
    event EventHandler<ServiceHostControl>? ServiceIconChanged;

    event EventHandler<HostFailedEventArgs>? HostFailed;
}
