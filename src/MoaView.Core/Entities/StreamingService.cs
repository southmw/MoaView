namespace MoaView.Core.Entities;

/// <summary>
/// 사이드바에 표시되는 서비스(사이트) 정보. 05 데이터베이스 설계 3.1절의 StreamingServices 테이블과 1:1로 대응한다.
/// Phase 1에서는 하드코딩 카탈로그가, Phase 2부터는 EF Core가 이 엔티티를 채운다.
/// </summary>
public sealed class StreamingService : ITimestamped
{
    public int Id { get; set; }

    /// <summary>고유 코드. 기본 제공은 netflix, tving, wavve, youtube, chzzk. 사용자 정의는 custom_{guid}.</summary>
    public required string Code { get; init; }

    public required string Name { get; set; }

    public required string HomeUrl { get; set; }

    public string? LoginUrl { get; set; }

    /// <summary>로그인 페이지 판별 정규식. 없으면 Provider 기본값을 쓴다.</summary>
    public string? LoginUrlPattern { get; set; }

    /// <summary>앱 안에서 탐색을 허용할 호스트 패턴 목록(예: *.netflix.com). 비어 있으면 Provider 기본값을 쓴다. DB에는 JSON 배열로 저장한다.</summary>
    public List<string> AllowedHosts { get; set; } = [];

    public string? IconPath { get; set; }

    /// <summary>사이드바 강조 색(#RRGGBB).</summary>
    public string? AccentColor { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>기본 제공 서비스는 삭제할 수 없고 숨길 수만 있다.</summary>
    public bool IsBuiltIn { get; set; }

    public string ProviderCode { get; set; } = "generic";

    public string? UserAgentOverride { get; set; }

    /// <summary>자동 입력 셀렉터(JSON). Provider 기본값보다 우선한다.</summary>
    public string? LoginFormSpecJson { get; set; }

    public double ZoomFactor { get; set; } = 1.0;

    public bool PauseOnSwitch { get; set; } = true;

    public string? LastUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<Account> Accounts { get; set; } = [];

    /// <summary>WebView2 프로필 이름. 계정별 프로필은 svc_{code}_{accountId}로 확장한다(D-11, Account.ProfileName).</summary>
    public string ProfileName => $"svc_{Code}";

    public const string GenericProviderCode = "generic";

    /// <summary>사용자 정의 서비스의 빈 초안(FR-SVC-05). 코드는 custom_{8자리 난수}, Provider는 generic. 이름·URL은 편집 대화상자가 채운다.</summary>
    public static StreamingService CreateCustom(int sortOrder) => new()
    {
        Code = "custom_" + Guid.NewGuid().ToString("N")[..8],
        Name = string.Empty,
        HomeUrl = string.Empty,
        ProviderCode = GenericProviderCode,
        AccentColor = "#3B82F6",
        SortOrder = sortOrder,
    };
}
