namespace MoaView.Core.Models;

/// <summary>
/// 로그인 자동 입력에 쓰는 셀렉터 모음(05 데이터베이스 설계 3.1절 LoginFormSpecJson).
/// 로그인 방식(native, naver, kakao ...)마다 폼 하나를 둔다. Phase 2에서 실제 값을 채운다.
/// </summary>
public sealed record LoginFormSpec(
    string? LoggedOutMarker,
    IReadOnlyDictionary<string, LoginForm> Forms);

/// <summary>로그인 폼 하나의 셀렉터. EntrySelector는 소셜 로그인에서 먼저 눌러야 하는 버튼이다.</summary>
public sealed record LoginForm(
    string IdSelector,
    string PasswordSelector,
    string SubmitSelector,
    string? EntrySelector = null);
