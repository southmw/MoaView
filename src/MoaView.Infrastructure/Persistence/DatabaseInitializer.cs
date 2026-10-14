using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoaView.Core.Abstractions;

namespace MoaView.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    IDbContextFactory<AppDbContext> factory,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("DB 마이그레이션 {Count}개 적용: {Names}", pending.Count, string.Join(", ", pending));
        }

        await db.Database.MigrateAsync(cancellationToken);

        var added = await DbSeeder.SeedAsync(db, cancellationToken);
        if (added > 0)
        {
            logger.LogInformation("기본 서비스 {Count}개 Seed", added);
        }
    }
}
