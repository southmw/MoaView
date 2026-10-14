using Microsoft.EntityFrameworkCore;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;

namespace MoaView.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository(IDbContextFactory<AppDbContext> factory) : IAccountRepository
{
    public async Task<IReadOnlyList<Account>> GetByServiceAsync(int serviceId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Accounts.AsNoTracking()
            .Where(a => a.ServiceId == serviceId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Account?> GetDefaultAsync(int serviceId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Accounts.AsNoTracking()
            .Where(a => a.ServiceId == serviceId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var siblings = await db.Accounts
            .Where(a => a.ServiceId == account.ServiceId && a.Id != account.Id)
            .ToListAsync(cancellationToken);

        // 첫 계정은 자동으로 기본 계정이 된다. 기본으로 지정하면 나머지는 해제한다(Partial Unique Index).
        if (siblings.Count == 0)
        {
            account.IsDefault = true;
        }
        else if (account.IsDefault)
        {
            foreach (var sibling in siblings.Where(s => s.IsDefault))
            {
                sibling.IsDefault = false;
            }
        }

        if (account.Id == 0)
        {
            db.Accounts.Add(account);
        }
        else
        {
            var entry = db.Accounts.Attach(account);
            entry.State = EntityState.Modified;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            return;
        }

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);

        // 기본 계정을 지웠으면 남은 계정 중 하나를 기본으로 올린다.
        if (account.IsDefault)
        {
            var next = await db.Accounts
                .Where(a => a.ServiceId == account.ServiceId)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (next is not null)
            {
                next.IsDefault = true;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
