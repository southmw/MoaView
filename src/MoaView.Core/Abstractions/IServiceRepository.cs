using MoaView.Core.Entities;

namespace MoaView.Core.Abstractions;

public interface IServiceRepository
{
    Task<IReadOnlyList<StreamingService>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<StreamingService?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<StreamingService?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(StreamingService service, CancellationToken cancellationToken = default);

    Task UpdateAsync(StreamingService service, CancellationToken cancellationToken = default);

    /// <summary>마지막 방문 URL만 갱신한다(FR-SVC-07). 전환마다 호출되므로 전체 엔티티를 저장하지 않는다.</summary>
    Task UpdateLastUrlAsync(int id, string? lastUrl, CancellationToken cancellationToken = default);

    /// <summary>서비스별 확대 배율만 갱신한다(FR-SET-03).</summary>
    Task UpdateZoomFactorAsync(int id, double zoomFactor, CancellationToken cancellationToken = default);

    /// <summary>표시 순서를 Id 목록 순서대로 1부터 다시 매긴다(FR-SVC-04). 목록에 없는 서비스는 그대로 둔다.</summary>
    Task ReorderAsync(IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default);

    /// <summary>캐시한 파비콘 경로만 갱신한다(FR-SVC-06). null이면 이니셜 아이콘으로 돌아간다.</summary>
    Task UpdateIconPathAsync(int id, string? iconPath, CancellationToken cancellationToken = default);

    /// <summary>활성화 여부만 갱신한다(FR-SVC-04).</summary>
    Task UpdateEnabledAsync(int id, bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>기본 제공 서비스는 삭제할 수 없다(IsBuiltIn). 호출자가 먼저 확인한다.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
