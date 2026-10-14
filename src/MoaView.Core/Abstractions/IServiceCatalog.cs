using MoaView.Core.Entities;

namespace MoaView.Core.Abstractions;

/// <summary>
/// 사이드바에 보여 줄 서비스 목록. Phase 1은 하드코딩, Phase 2부터 SQLite 저장소가 구현한다(07 개발 계획).
/// </summary>
public interface IServiceCatalog
{
    Task<IReadOnlyList<StreamingService>> GetEnabledAsync(CancellationToken cancellationToken = default);
}
