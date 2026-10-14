using System.Text.Json;
using System.Text.RegularExpressions;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;
using MoaView.Core.Models;

namespace MoaView.Infrastructure.Providers;

/// <summary>
/// Provider 공통 동작. 파생 클래스는 코드, 기본 허용 호스트, 로그인 URL 패턴만 정한다.
/// DB(StreamingService)에 값이 있으면 그 값이 우선한다(NFR-08: 사이트가 바뀌면 데이터만 고친다).
/// </summary>
public abstract class StreamingProviderBase : IStreamingProvider
{
    /// <summary>소셜·제휴 로그인 팝업이 뜨는 호스트. 앱 안의 OAuth 대화상자로 연다.</summary>
    private static readonly string[] OAuthHosts =
    [
        "accounts.google.com",
        "accounts.youtube.com",
        "*.nid.naver.com",
        "nid.naver.com",
        "kauth.kakao.com",
        "accounts.kakao.com",
        "logins.daum.net",
        "*.cjone.com",
        "appleid.apple.com",
        "www.facebook.com",
        "m.facebook.com",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public abstract string Code { get; }

    /// <summary>DB에 AllowedHosts가 없을 때 쓰는 기본값.</summary>
    protected abstract IReadOnlyList<string> DefaultAllowedHosts { get; }

    /// <summary>DB에 LoginUrlPattern이 없을 때 쓰는 기본 로그인 페이지 정규식. null이면 로그인 페이지를 판별하지 않는다.</summary>
    protected virtual Regex? DefaultLoginUrlRegex => null;

    protected virtual LoginFormSpec? DefaultLoginFormSpec => null;

    protected virtual string? DefaultLoginUrl => null;

    public virtual string PrimaryLoginProvider => LoginProviders.Native;

    public virtual bool CanHandle(StreamingService service) =>
        string.Equals(service.ProviderCode, Code, StringComparison.OrdinalIgnoreCase);

    public virtual IReadOnlyList<string> GetAllowedHosts(StreamingService service) =>
        service.AllowedHosts.Count > 0 ? service.AllowedHosts : DefaultAllowedHosts;

    public string? GetLoginUrl(StreamingService service) =>
        string.IsNullOrWhiteSpace(service.LoginUrl) ? DefaultLoginUrl : service.LoginUrl;

    public virtual bool IsLoginPage(StreamingService service, Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(service.LoginUrlPattern))
        {
            try
            {
                return Regex.IsMatch(uri.AbsoluteUri, service.LoginUrlPattern,
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException)
            {
                // 잘못된 정규식은 무시하고 기본값으로 넘어간다.
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        return DefaultLoginUrlRegex?.IsMatch(uri.AbsoluteUri) ?? false;
    }

    public virtual LoginFormSpec? GetLoginFormSpec(StreamingService service)
    {
        if (!string.IsNullOrWhiteSpace(service.LoginFormSpecJson))
        {
            try
            {
                return JsonSerializer.Deserialize<LoginFormSpec>(service.LoginFormSpecJson, JsonOptions);
            }
            catch (JsonException)
            {
                // 깨진 JSON은 Provider 기본값으로 대체한다.
            }
        }

        return DefaultLoginFormSpec;
    }

    public virtual NewWindowPolicy ResolveNewWindow(StreamingService service, Uri target)
    {
        if (!target.IsAbsoluteUri)
        {
            return NewWindowPolicy.InApp;
        }

        if (HostPattern.MatchesAny(GetAllowedHosts(service), target))
        {
            return NewWindowPolicy.InApp;
        }

        if (HostPattern.MatchesAny(OAuthHosts, target))
        {
            return NewWindowPolicy.OAuthDialog;
        }

        return NewWindowPolicy.ExternalBrowser;
    }
}
