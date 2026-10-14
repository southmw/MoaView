using MoaView.Core.Entities;

namespace MoaView.Core.Abstractions;

/// <summary>서비스의 ProviderCode에 맞는 Provider를 찾는다. 없으면 GenericProvider.</summary>
public interface IStreamingProviderResolver
{
    IStreamingProvider Resolve(StreamingService service);
}
