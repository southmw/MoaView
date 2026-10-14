using MoaView.Core.Abstractions;
using MoaView.Core.Entities;

namespace MoaView.Infrastructure.Providers;

public sealed class StreamingProviderResolver : IStreamingProviderResolver
{
    private readonly IReadOnlyDictionary<string, IStreamingProvider> _byCode;
    private readonly IStreamingProvider _fallback;

    public StreamingProviderResolver(IEnumerable<IStreamingProvider> providers)
    {
        var list = providers.ToList();
        _byCode = list.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        _fallback = list.FirstOrDefault(p => p.Code == GenericProvider.ProviderCode)
                    ?? throw new InvalidOperationException("GenericProvider가 등록되어 있지 않습니다.");
    }

    public IStreamingProvider Resolve(StreamingService service)
    {
        if (_byCode.TryGetValue(service.ProviderCode, out var provider) && provider.CanHandle(service))
        {
            return provider;
        }

        return _fallback;
    }
}
