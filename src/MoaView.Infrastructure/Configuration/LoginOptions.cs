namespace MoaView.Infrastructure.Configuration;

/// <summary>로그인 보조 옵션(03 기술 검토 4.5절, FR-ACC-10). appsettings.json의 "Login" 절.</summary>
public sealed class LoginOptions
{
    public const string SectionName = "Login";

    /// <summary>같은 계정의 자동 제출 최소 간격(분).</summary>
    public int AutoSubmitCooldownMinutes { get; set; } = 10;

    /// <summary>연속 실패가 이 횟수에 이르면 자동 제출을 끈다.</summary>
    public int MaxConsecutiveFailures { get; set; } = 2;

    /// <summary>SPA가 로그인 폼을 늦게 그릴 수 있어 자동 입력을 이 시간(초) 동안 재시도한다.</summary>
    public int FillRetrySeconds { get; set; } = 8;

    /// <summary>자동 제출 뒤 로그인 페이지를 벗어나는지 지켜보는 시간(초). 넘기면 실패로 본다.</summary>
    public int SubmitJudgeSeconds { get; set; } = 20;
}
