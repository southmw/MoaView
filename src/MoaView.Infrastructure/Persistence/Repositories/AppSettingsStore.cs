using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;

namespace MoaView.Infrastructure.Persistence.Repositories;

public sealed class AppSettingsStore(IDbContextFactory<AppDbContext> factory) : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var (found, value) = await TryGetAsync<T>(key, cancellationToken);
        return found ? value : default;
    }

    /// <summary>키가 없거나 값을 읽을 수 없으면 기본값. 값 형식(int, bool)의 default(0, false)와 "없음"을 구분한다.</summary>
    public async Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        var (found, value) = await TryGetAsync<T>(key, cancellationToken);
        return found && value is not null ? value : defaultValue;
    }

    private async Task<(bool Found, T? Value)> TryGetAsync<T>(string key, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var setting = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting?.Value is null)
        {
            return (false, default);
        }

        try
        {
            return (true, JsonSerializer.Deserialize<T>(setting.Value, JsonOptions));
        }
        catch (JsonException)
        {
            return (false, default);
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
        {
            db.Settings.Add(new AppSetting { Key = key, Value = json });
        }
        else
        {
            setting.Value = json;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Settings.Where(s => s.Key == key).ExecuteDeleteAsync(cancellationToken);
    }
}
