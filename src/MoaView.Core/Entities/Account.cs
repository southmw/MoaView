namespace MoaView.Core.Entities;

/// <summary>
/// 서비스별 로그인 정보(05 데이터베이스 설계 3.2절). 비밀번호는 DPAPI로 암호화한 바이트만 저장한다(FR-ACC-02).
/// 1차(개인용)는 서비스당 계정 1개만 쓰지만 스키마는 다중 계정을 수용한다(D-11).
/// </summary>
public sealed class Account : ITimestamped
{
    public int Id { get; set; }

    public int ServiceId { get; set; }

    public StreamingService? Service { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>로그인 ID 또는 이메일. 표시용이라 평문이다.</summary>
    public required string LoginId { get; set; }

    /// <summary>DPAPI로 암호화한 비밀번호. null이면 세션(쿠키)만 쓴다.</summary>
    public byte[]? EncryptedPassword { get; set; }

    /// <summary>로그인 방식: native(서비스 자체 ID) / cjone / naver / kakao / google / tid 등. 어떤 LoginForm을 쓸지 정한다.</summary>
    public string LoginProvider { get; set; } = LoginProviders.Native;

    /// <summary>WebView2 프로필 이름(svc_{code}_{id}). 서비스당 1개일 때는 서비스 프로필과 같다.</summary>
    public required string ProfileName { get; set; }

    public bool IsDefault { get; set; }

    public bool AutoFillEnabled { get; set; } = true;

    /// <summary>로그인 버튼까지 자동으로 누른다. 계정 잠금 위험 때문에 기본은 끔(D-09).</summary>
    public bool AutoSubmitEnabled { get; set; }

    public DateTime? LastLoginAt { get; set; }

    /// <summary>LoginGuard의 쿨다운 기준 시각(FR-ACC-10).</summary>
    public DateTime? LastAutoSubmitAt { get; set; }

    /// <summary>연속 자동 로그인 실패 횟수. 성공하거나 비밀번호를 고치면 0.</summary>
    public int LoginFailCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public static class LoginProviders
{
    public const string Native = "native";
    public const string CjOne = "cjone";
    public const string Naver = "naver";
    public const string Kakao = "kakao";
    public const string Google = "google";
    public const string TId = "tid";
}
