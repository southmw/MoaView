using System.IO;
using Microsoft.EntityFrameworkCore;
using MoaView.Core.Abstractions;

namespace MoaView.Infrastructure.Persistence;

public sealed class DatabaseMaintenance(IDbContextFactory<AppDbContext> factory) : IDatabaseMaintenance
{
    public async Task BackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        // VACUUM INTO는 트랜잭션 밖에서 실행해야 하며, 쓰기 중인 DB도 일관된 스냅샷으로 복사한다. 경로는 매개변수로 넘긴다.
        await db.Database.ExecuteSqlInterpolatedAsync($"VACUUM INTO {destinationPath}", cancellationToken);
    }
}
