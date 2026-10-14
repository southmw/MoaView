using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoaView.Infrastructure.Persistence;

namespace MoaView.Infrastructure.Tests;

/// <summary>
/// 테스트마다 격리된 SQLite in-memory DB. 연결을 열어 둔 동안만 DB가 살아 있으므로 팩터리가 같은 연결을 공유한다.
/// 마이그레이션을 실제로 적용해서 Migrations 폴더의 스키마도 함께 검증한다.
/// </summary>
public sealed class SqliteTestDatabase : IDbContextFactory<AppDbContext>, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    private SqliteTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public static async Task<SqliteTestDatabase> CreateAsync(bool seed = false)
    {
        var database = new SqliteTestDatabase();
        await using var db = database.CreateDbContext();
        await db.Database.MigrateAsync();
        if (seed)
        {
            await DbSeeder.SeedAsync(db);
        }

        return database;
    }

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
