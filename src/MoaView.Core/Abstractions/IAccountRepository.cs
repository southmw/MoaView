using MoaView.Core.Entities;

namespace MoaView.Core.Abstractions;

public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> GetByServiceAsync(int serviceId, CancellationToken cancellationToken = default);

    Task<Account?> GetDefaultAsync(int serviceId, CancellationToken cancellationToken = default);

    Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>추가 또는 수정. IsDefault가 true면 같은 서비스의 다른 계정은 기본에서 해제한다.</summary>
    Task SaveAsync(Account account, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
