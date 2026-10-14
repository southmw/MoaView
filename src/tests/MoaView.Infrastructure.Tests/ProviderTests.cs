using FluentAssertions;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;
using MoaView.Core.Models;
using MoaView.Infrastructure.Providers;
using Xunit;

namespace MoaView.Infrastructure.Tests;

public class ProviderTests
{
    private static StreamingService Service(string code, string home, string provider) => new()
    {
        Code = code, Name = code, HomeUrl = home, ProviderCode = provider,
    };

    [Theory]
    [InlineData("https://www.netflix.com/title/123", NewWindowPolicy.InApp)]
    [InlineData("https://help.netflix.com/ko", NewWindowPolicy.InApp)]
    [InlineData("https://accounts.google.com/o/oauth2/auth", NewWindowPolicy.OAuthDialog)]
    [InlineData("https://www.example.com/", NewWindowPolicy.ExternalBrowser)]
    public void Netflix_resolves_new_window_policy(string url, NewWindowPolicy expected)
    {
        var provider = new NetflixProvider();
        var service = Service("netflix", "https://www.netflix.com/browse", "netflix");

        provider.ResolveNewWindow(service, new Uri(url)).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://www.netflix.com/kr/login?nextpage=x", true)]
    [InlineData("https://www.netflix.com/login", true)]
    [InlineData("https://www.netflix.com/browse", false)]
    public void Netflix_detects_login_page(string url, bool expected)
    {
        var provider = new NetflixProvider();
        var service = Service("netflix", "https://www.netflix.com/browse", "netflix");

        provider.IsLoginPage(service, new Uri(url)).Should().Be(expected);
    }

    [Fact]
    public void Db_login_pattern_overrides_provider_default()
    {
        var provider = new TvingProvider();
        var service = Service("tving", "https://www.tving.com", "tving");
        service.LoginUrlPattern = @"^https://www\.tving\.com/custom-login";

        provider.IsLoginPage(service, new Uri("https://www.tving.com/custom-login")).Should().BeTrue();
        provider.IsLoginPage(service, new Uri("https://www.tving.com/account/login")).Should().BeFalse();
    }

    [Fact]
    public void Db_allowed_hosts_override_provider_default()
    {
        var provider = new WavveProvider();
        var service = Service("wavve", "https://www.wavve.com", "wavve");
        service.AllowedHosts = ["*.custom.example"];

        provider.ResolveNewWindow(service, new Uri("https://www.wavve.com/")).Should().Be(NewWindowPolicy.ExternalBrowser);
        provider.ResolveNewWindow(service, new Uri("https://a.custom.example/")).Should().Be(NewWindowPolicy.InApp);
    }

    [Fact]
    public void Chzzk_treats_naver_login_as_in_app_because_it_is_an_allowed_host()
    {
        var provider = new ChzzkProvider();
        var service = Service("chzzk", "https://chzzk.naver.com", "chzzk");

        provider.ResolveNewWindow(service, new Uri("https://nid.naver.com/nidlogin.login")).Should().Be(NewWindowPolicy.InApp);
        provider.IsLoginPage(service, new Uri("https://nid.naver.com/nidlogin.login?url=x")).Should().BeTrue();
    }

    [Fact]
    public void Generic_provider_derives_allowed_hosts_from_home_url()
    {
        var provider = new GenericProvider();
        var service = Service("custom_1", "https://www.coupangplay.com/", GenericProvider.ProviderCode);

        provider.GetAllowedHosts(service).Should().Equal("*.coupangplay.com");
        service.AllowedHosts = ["*.custom.example"];
        provider.GetAllowedHosts(service).Should().Equal(["*.custom.example"], "DB 값이 있으면 그것이 우선한다");
        service.AllowedHosts = [];

        provider.ResolveNewWindow(service, new Uri("https://api.coupangplay.com/x")).Should().Be(NewWindowPolicy.InApp);
        provider.ResolveNewWindow(service, new Uri("https://kauth.kakao.com/oauth")).Should().Be(NewWindowPolicy.OAuthDialog);
        provider.ResolveNewWindow(service, new Uri("https://news.example.com/")).Should().Be(NewWindowPolicy.ExternalBrowser);
    }

    [Fact]
    public void Login_form_spec_is_parsed_from_db_json()
    {
        var provider = new TvingProvider();
        var service = Service("tving", "https://www.tving.com", "tving");
        service.LoginFormSpecJson = """
            {
              "loggedOutMarker": "a[href*='/login']",
              "forms": {
                "native": { "idSelector": "#id", "passwordSelector": "#pw", "submitSelector": "button[type=submit]" }
              }
            }
            """;

        var spec = provider.GetLoginFormSpec(service);

        spec.Should().NotBeNull();
        spec!.LoggedOutMarker.Should().Be("a[href*='/login']");
        spec.Forms.Should().ContainKey("native");
        spec.Forms["native"].IdSelector.Should().Be("#id");
    }

    [Fact]
    public void Login_url_prefers_db_value_over_provider_default()
    {
        var provider = new TvingProvider();
        var service = Service("tving", "https://www.tving.com", "tving");

        provider.GetLoginUrl(service).Should().Be("https://www.tving.com/account/login");

        service.LoginUrl = "https://www.tving.com/custom";
        provider.GetLoginUrl(service).Should().Be("https://www.tving.com/custom");

        new YouTubeProvider().GetLoginUrl(Service("youtube", "https://www.youtube.com", "youtube")).Should().StartWith("https://accounts.google.com/");
    }

    [Fact]
    public void Primary_login_provider_reflects_parent_identity()
    {
        new TvingProvider().PrimaryLoginProvider.Should().Be(LoginProviders.Native);
        new ChzzkProvider().PrimaryLoginProvider.Should().Be(LoginProviders.Naver);
        new YouTubeProvider().PrimaryLoginProvider.Should().Be(LoginProviders.Google);
    }

    [Fact]
    public void Resolver_falls_back_to_generic_for_unknown_provider_code()
    {
        IStreamingProvider[] providers = [new NetflixProvider(), new GenericProvider()];
        var resolver = new StreamingProviderResolver(providers);

        resolver.Resolve(Service("netflix", "https://www.netflix.com", "netflix")).Should().BeOfType<NetflixProvider>();
        resolver.Resolve(Service("x", "https://x.example", "does-not-exist")).Should().BeOfType<GenericProvider>();
    }
}
