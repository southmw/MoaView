namespace MoaView.Core.Models;

/// <summary>
/// AllowedHosts 패턴 매칭. "*.netflix.com"은 netflix.com과 모든 하위 도메인에, "www.tving.com"은 그 호스트에만 맞는다.
/// 대소문자를 구분하지 않는다.
/// </summary>
public static class HostPattern
{
    public static bool Matches(string pattern, string host)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        pattern = pattern.Trim();
        host = host.Trim().TrimEnd('.');

        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            var root = pattern[2..];
            return host.Equals(root, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + root, StringComparison.OrdinalIgnoreCase);
        }

        return host.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesAny(IEnumerable<string> patterns, string host) =>
        patterns.Any(p => Matches(p, host));

    public static bool MatchesAny(IEnumerable<string> patterns, Uri uri) =>
        uri.IsAbsoluteUri && MatchesAny(patterns, uri.Host);
}
