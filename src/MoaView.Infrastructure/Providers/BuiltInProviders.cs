using System.Text.RegularExpressions;
using MoaView.Core.Entities;
using MoaView.Core.Models;

namespace MoaView.Infrastructure.Providers;

/// <summary>
/// 기본 제공 서비스 5종의 Provider. 로그인 폼 셀렉터(LoginFormSpec)는 두지 않는다.
/// 자동 입력은 "보이는 비밀번호 입력란 + 그 앞의 ID 입력란" 휴리스틱(autofill.js)으로 동작하고,
/// 사이트별로 필요하면 DB의 LoginFormSpecJson에 셀렉터를 넣어 우선 적용한다(NFR-08).
/// </summary>
public sealed partial class NetflixProvider : StreamingProviderBase
{
    public override string Code => "netflix";

    protected override IReadOnlyList<string> DefaultAllowedHosts { get; } =
        ["*.netflix.com", "*.nflxext.com", "*.nflximg.net", "*.nflxvideo.net", "*.nflxso.net"];

    protected override Regex? DefaultLoginUrlRegex => LoginRegex();

    protected override string? DefaultLoginUrl => "https://www.netflix.com/login";

    [GeneratedRegex(@"^https://www\.netflix\.com/(?:[a-z]{2}(?:-[a-z]{2})?/)?login", RegexOptions.IgnoreCase)]
    private static partial Regex LoginRegex();
}

public sealed partial class TvingProvider : StreamingProviderBase
{
    public override string Code => "tving";

    protected override IReadOnlyList<string> DefaultAllowedHosts { get; } =
        ["*.tving.com", "*.cjenm.com", "*.cj.net", "*.cjone.com"];

    protected override Regex? DefaultLoginUrlRegex => LoginRegex();

    protected override string? DefaultLoginUrl => "https://www.tving.com/account/login";

    [GeneratedRegex(@"^https://www\.tving\.com/account/login", RegexOptions.IgnoreCase)]
    private static partial Regex LoginRegex();
}

public sealed partial class WavveProvider : StreamingProviderBase
{
    public override string Code => "wavve";

    protected override IReadOnlyList<string> DefaultAllowedHosts { get; } =
        ["*.wavve.com", "*.pooq.co.kr"];

    protected override Regex? DefaultLoginUrlRegex => LoginRegex();

    protected override string? DefaultLoginUrl => "https://www.wavve.com/login";

    [GeneratedRegex(@"^https://www\.wavve\.com/(?:login|member/login|auth/login)", RegexOptions.IgnoreCase)]
    private static partial Regex LoginRegex();
}

public sealed partial class YouTubeProvider : StreamingProviderBase
{
    public override string Code => "youtube";

    protected override IReadOnlyList<string> DefaultAllowedHosts { get; } =
    [
        "*.youtube.com", "*.google.com", "*.googlevideo.com", "*.ytimg.com", "*.ggpht.com",
        "*.googleusercontent.com", "*.gstatic.com", "*.youtube-nocookie.com",
    ];

    protected override Regex? DefaultLoginUrlRegex => LoginRegex();

    // Google 로그인은 2단계 인증이 흔해 자동 입력 효과가 제한적이지만(03 기술 검토 4.5절), 이메일·비밀번호 단계까지는 채워 준다.
    protected override string? DefaultLoginUrl =>
        "https://accounts.google.com/ServiceLogin?service=youtube&continue=https%3A%2F%2Fwww.youtube.com%2F";

    public override string PrimaryLoginProvider => LoginProviders.Google;

    [GeneratedRegex(@"^https://accounts\.google\.com/", RegexOptions.IgnoreCase)]
    private static partial Regex LoginRegex();
}

public sealed partial class ChzzkProvider : StreamingProviderBase
{
    public override string Code => "chzzk";

    protected override IReadOnlyList<string> DefaultAllowedHosts { get; } =
        ["*.naver.com", "*.naver.net", "*.pstatic.net", "*.navercorp.com", "*.chzzk.naver.com"];

    protected override Regex? DefaultLoginUrlRegex => LoginRegex();

    protected override string? DefaultLoginUrl => "https://nid.naver.com/nidlogin.login?url=https%3A%2F%2Fchzzk.naver.com%2F";

    public override string PrimaryLoginProvider => LoginProviders.Naver;

    [GeneratedRegex(@"^https://nid\.naver\.com/nidlogin\.login", RegexOptions.IgnoreCase)]
    private static partial Regex LoginRegex();
}

/// <summary>
/// 사용자 정의 서비스용(FR-SVC-05). 허용 호스트는 DB 값이 없으면 홈 URL의 호스트(www. 제외)와 그 하위 도메인으로 한다.
/// 이 값이 새 창 정책, 마지막 URL 저장(PersistableUrl), 자동 입력 허용 판정에 모두 쓰인다.
/// </summary>
public sealed class GenericProvider : StreamingProviderBase
{
    public const string ProviderCode = StreamingService.GenericProviderCode;

    public override string Code => ProviderCode;

    protected override IReadOnlyList<string> DefaultAllowedHosts => [];

    public override bool CanHandle(StreamingService service) => true;

    public override IReadOnlyList<string> GetAllowedHosts(StreamingService service)
    {
        if (service.AllowedHosts.Count > 0)
        {
            return service.AllowedHosts;
        }

        if (Uri.TryCreate(service.HomeUrl, UriKind.Absolute, out var home) && home.Host.Length > 0)
        {
            var root = home.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? home.Host[4..] : home.Host;
            return ["*." + root];
        }

        return [];
    }
}
