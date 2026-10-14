using Microsoft.Extensions.Logging;
using MoaView.App.Controls;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;
using MoaView.Infrastructure.WebView;

namespace MoaView.App.Services;

/// <summary>ServiceHostControl은 서비스마다 하나씩 만들어지므로 DI 컨테이너 대신 팩터리가 의존성을 주입한다.</summary>
public sealed class ServiceHostFactory(
    IStreamingProviderResolver resolver,
    IWebView2EnvironmentProvider environmentProvider,
    ILoggerFactory loggerFactory)
{
    public ServiceHostControl Create(StreamingService service) =>
        new(service, resolver.Resolve(service), environmentProvider, loggerFactory.CreateLogger<ServiceHostControl>());
}
