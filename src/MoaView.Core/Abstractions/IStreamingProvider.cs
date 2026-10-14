using MoaView.Core.Entities;
using MoaView.Core.Models;

namespace MoaView.Core.Abstractions;

/// <summary>
/// 서비스별로 다른 동작(로그인 감지, 자동 입력 셀렉터, 새 창 정책)을 캡슐화한다(04 아키텍처 4.1절, NFR-08).
/// 기본 서비스는 전용 Provider를, 사용자 정의 서비스는 GenericProvider를 쓴다.
/// DB(StreamingService)에 값이 있으면 그 값이 Provider 기본값보다 우선한다.
/// </summary>
public interface IStreamingProvider
{
    /// <summary>"netflix", "tving", ... "generic".</summary>
    string Code { get; }

    bool CanHandle(StreamingService service);

    /// <summary>현재 URL이 로그인 페이지인지 판단한다.</summary>
    bool IsLoginPage(StreamingService service, Uri uri);

    /// <summary>자동 입력 셀렉터. 아직 분석되지 않은 서비스는 null.</summary>
    LoginFormSpec? GetLoginFormSpec(StreamingService service);

    /// <summary>새 창 요청 처리 정책(FR-NAV-04).</summary>
    NewWindowPolicy ResolveNewWindow(StreamingService service, Uri target);

    /// <summary>앱 안에서 탐색을 허용할 호스트 패턴. DB 값이 비어 있으면 Provider 기본값.</summary>
    IReadOnlyList<string> GetAllowedHosts(StreamingService service);

    /// <summary>로그아웃 상태를 감지했을 때 이동할 로그인 페이지(FR-ACC-08). DB의 LoginUrl이 우선. 없으면 null.</summary>
    string? GetLoginUrl(StreamingService service);

    /// <summary>
    /// 이 서비스의 기본 로그인 방식(LoginProviders 상수). 대부분 native지만 치지직은 naver, YouTube는 google처럼
    /// 서비스 자체 로그인이 곧 상위 계정 로그인인 경우가 있다. 이 값과 같은 LoginProvider는 자체 ID로 취급한다.
    /// </summary>
    string PrimaryLoginProvider { get; }
}
