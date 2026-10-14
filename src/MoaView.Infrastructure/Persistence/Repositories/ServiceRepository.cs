using Microsoft.EntityFrameworkCore;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;

namespace MoaView.Infrastructure.Persistence.Repositories;

/// <summary>StreamingServices 저장소. IServiceCatalog(사이드바 목록)도 함께 구현한다.</summary>
public sealed class ServiceRepository(IDbContextFactory<AppDbContext> factory) : IServiceRepository, IServiceCatalog
{
    public async Task<IReadOnlyList<StreamingService>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Services.AsNoTracking()
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StreamingService>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Services.AsNoTracking()
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<StreamingService?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<StreamingService?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Code == code, cancellationToken);
    }

    public async Task AddAsync(StreamingService service, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.Services.Add(service);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(StreamingService service, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entry = db.Services.Attach(service);
        entry.State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLastUrlAsync(int id, string? lastUrl, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Services
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastUrl, lastUrl)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task UpdateZoomFactorAsync(int id, double zoomFactor, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Services
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ZoomFactor, zoomFactor)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task ReorderAsync(IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var services = await db.Services.Where(s => orderedIds.Contains(s.Id)).ToListAsync(cancellationToken);
        foreach (var (id, index) in orderedIds.Select((id, i) => (id, i)))
        {
            var service = services.FirstOrDefault(s => s.Id == id);
            if (service is not null)
            {
                service.SortOrder = index + 1;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateIconPathAsync(int id, string? iconPath, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Services
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IconPath, iconPath)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task UpdateEnabledAsync(int id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Services
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsEnabled, isEnabled)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null)
        {
            return;
        }

        if (service.IsBuiltIn)
        {
            throw new InvalidOperationException($"기본 제공 서비스({service.Code})는 삭제할 수 없습니다. 숨김만 가능합니다.");
        }

        db.Services.Remove(service);
        await db.SaveChangesAsync(cancellationToken);
    }
}
