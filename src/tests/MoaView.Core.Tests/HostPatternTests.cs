using FluentAssertions;
using MoaView.Core.Models;
using Xunit;

namespace MoaView.Core.Tests;

public class HostPatternTests
{
    [Theory]
    [InlineData("*.netflix.com", "www.netflix.com", true)]
    [InlineData("*.netflix.com", "netflix.com", true)]
    [InlineData("*.netflix.com", "assets.cdn.netflix.com", true)]
    [InlineData("*.netflix.com", "WWW.NETFLIX.COM", true)]
    [InlineData("*.netflix.com", "notnetflix.com", false)]
    [InlineData("*.netflix.com", "netflix.com.evil.example", false)]
    [InlineData("www.tving.com", "www.tving.com", true)]
    [InlineData("www.tving.com", "api.tving.com", false)]
    [InlineData("", "www.tving.com", false)]
    [InlineData("*.tving.com", "", false)]
    public void Matches_follows_wildcard_rules(string pattern, string host, bool expected)
    {
        HostPattern.Matches(pattern, host).Should().Be(expected);
    }

    [Fact]
    public void MatchesAny_accepts_uri_and_checks_every_pattern()
    {
        string[] patterns = ["*.youtube.com", "accounts.google.com"];

        HostPattern.MatchesAny(patterns, new Uri("https://accounts.google.com/signin")).Should().BeTrue();
        HostPattern.MatchesAny(patterns, new Uri("https://m.youtube.com/")).Should().BeTrue();
        HostPattern.MatchesAny(patterns, new Uri("https://accounts.google.co.kr/")).Should().BeFalse();
    }

    [Fact]
    public void MatchesAny_rejects_relative_uri()
    {
        HostPattern.MatchesAny(["*.example.com"], new Uri("/path", UriKind.Relative)).Should().BeFalse();
    }
}
