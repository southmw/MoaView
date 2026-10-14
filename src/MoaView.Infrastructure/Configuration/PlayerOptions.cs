namespace MoaView.Infrastructure.Configuration;

/// <summary>
/// 재생·자원 관리 옵션. Phase 1은 appsettings.json의 "Player" 절에서 읽고, Phase 2부터 AppSettings 테이블로 옮긴다(05 데이터베이스 설계 3.3절).
/// </summary>
public sealed class PlayerOptions
{
    public const string SectionName = "Player";

    /// <summary>비활성 서비스를 Suspend하기까지 기다리는 시간(초). PoC에서 재생 중 서비스 하나가 약 1 GB를 썼으므로 필수다.</summary>
    public int SuspendAfterSeconds { get; set; } = 300;

    /// <summary>동시에 살려 둘 WebView 최대 개수(LRU).</summary>
    public int MaxAliveHosts { get; set; } = 3;

    /// <summary>다른 서비스로 전환할 때 이전 서비스의 재생을 일시정지한다(FR-PLY-04).</summary>
    public bool PauseOnSwitch { get; set; } = true;
}
