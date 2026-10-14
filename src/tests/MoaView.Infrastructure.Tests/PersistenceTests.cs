using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MoaView.Core.Entities;
using MoaView.Core.Models;
using MoaView.Infrastructure.Persistence;
using MoaView.Infrastructure.Persistence.Repositories;
using Xunit;

namespace MoaView.Infrastructure.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task Seeder_adds_five_builtin_services_once()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();

        var first = await DbSeeder.SeedAsync(db);
        var second = await DbSeeder.SeedAsync(db);

        first.Should().Be(5);
        second.Should().Be(0);
        var services = await db.Services.OrderBy(s => s.SortOrder).ToListAsync();
        services.Select(s => s.Code).Should().Equal("netflix", "tving", "wavve", "youtube", "chzzk");
        services.Should().OnlyContain(s => s.IsBuiltIn && s.IsEnabled && s.CreatedAt != default);
    }

    [Fact]
    public async Task Seeder_keeps_user_changes_to_existing_services()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var repository = new ServiceRepository(database);

        var tving = (await repository.GetByCodeAsync("tving"))!;
        tving.IsEnabled = false;
        tving.AllowedHosts = ["*.tving.com", "*.custom.example"];
        await repository.UpdateAsync(tving);

        await using (var db = database.CreateDbContext())
        {
            await DbSeeder.SeedAsync(db);
        }

        var reloaded = (await repository.GetByCodeAsync("tving"))!;
        reloaded.IsEnabled.Should().BeFalse();
        reloaded.AllowedHosts.Should().Equal("*.tving.com", "*.custom.example");
        (await repository.GetEnabledAsync()).Should().HaveCount(4);
    }

    [Fact]
    public async Task UpdateLastUrl_changes_only_that_column()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var repository = new ServiceRepository(database);
        var netflix = (await repository.GetByCodeAsync("netflix"))!;

        await repository.UpdateLastUrlAsync(netflix.Id, "https://www.netflix.com/watch/1");

        var reloaded = (await repository.GetByIdAsync(netflix.Id))!;
        reloaded.LastUrl.Should().Be("https://www.netflix.com/watch/1");
        reloaded.Name.Should().Be("Netflix");
        reloaded.UpdatedAt.Should().BeOnOrAfter(reloaded.CreatedAt);
    }

    [Fact]
    public async Task UpdateZoomFactor_changes_only_that_column()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var repository = new ServiceRepository(database);
        var tving = (await repository.GetByCodeAsync("tving"))!;

        await repository.UpdateZoomFactorAsync(tving.Id, 1.25);

        var reloaded = (await repository.GetByIdAsync(tving.Id))!;
        reloaded.ZoomFactor.Should().Be(1.25);
        reloaded.LastUrl.Should().BeNull();
    }

    [Fact]
    public async Task UpdateIconPath_changes_only_that_column()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var repository = new ServiceRepository(database);
        var chzzk = (await repository.GetByCodeAsync("chzzk"))!;

        await repository.UpdateIconPathAsync(chzzk.Id, @"C:\icons\chzzk.png");
        (await repository.GetByIdAsync(chzzk.Id))!.IconPath.Should().Be(@"C:\icons\chzzk.png");

        await repository.UpdateIconPathAsync(chzzk.Id, null);
        var reloaded = (await repository.GetByIdAsync(chzzk.Id))!;
        reloaded.IconPath.Should().BeNull();
        reloaded.Name.Should().Be("치지직");
    }

    [Fact]
    public async Task Builtin_service_cannot_be_deleted_but_custom_can()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var repository = new ServiceRepository(database);
        var custom = new StreamingService { Code = "custom_1", Name = "Custom", HomeUrl = "https://example.com" };
        await repository.AddAsync(custom);

        var netflix = (await repository.GetByCodeAsync("netflix"))!;
        var act = () => repository.DeleteAsync(netflix.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await repository.DeleteAsync(custom.Id);
        (await repository.GetByCodeAsync("custom_1")).Should().BeNull();
    }

    [Fact]
    public async Task Reorder_renumbers_sort_order_from_one()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var repository = new ServiceRepository(database);
        var ids = (await repository.GetAllAsync()).Select(s => s.Id).Reverse().ToList();

        await repository.ReorderAsync(ids);

        var reloaded = await repository.GetAllAsync();
        reloaded.Select(s => s.Code).Should().Equal("chzzk", "youtube", "wavve", "tving", "netflix");
        reloaded.Select(s => s.SortOrder).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task UpdateEnabled_hides_service_from_catalog()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var repository = new ServiceRepository(database);
        var wavve = (await repository.GetByCodeAsync("wavve"))!;

        await repository.UpdateEnabledAsync(wavve.Id, false);

        (await repository.GetEnabledAsync()).Select(s => s.Code).Should().NotContain("wavve");
        (await repository.GetAllAsync()).Should().HaveCount(5);
    }

    [Fact]
    public async Task Backup_copies_database_to_file()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var maintenance = new DatabaseMaintenance(database);
        var path = Path.Combine(Path.GetTempPath(), $"moaview-backup-{Guid.NewGuid():N}.db");
        try
        {
            await maintenance.BackupAsync(path);

            File.Exists(path).Should().BeTrue();
            await using var copy = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options);
            (await copy.Services.CountAsync()).Should().Be(5);
        }
        finally
        {
            // 연결 풀이 파일을 잡고 있을 수 있어 풀을 비운 뒤 지운다.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task First_account_becomes_default_and_default_is_exclusive()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var services = new ServiceRepository(database);
        var accounts = new AccountRepository(database);
        var tving = (await services.GetByCodeAsync("tving"))!;

        var first = new Account { ServiceId = tving.Id, DisplayName = "내 계정", LoginId = "me", ProfileName = "svc_tving_1" };
        await accounts.SaveAsync(first);
        var second = new Account { ServiceId = tving.Id, DisplayName = "가족", LoginId = "family", ProfileName = "svc_tving_2", IsDefault = true };
        await accounts.SaveAsync(second);

        var list = await accounts.GetByServiceAsync(tving.Id);
        list.Should().HaveCount(2);
        list.Single(a => a.IsDefault).LoginId.Should().Be("family");
        (await accounts.GetDefaultAsync(tving.Id))!.LoginId.Should().Be("family");
    }

    [Fact]
    public async Task Deleting_default_account_promotes_another()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(seed: true);
        var services = new ServiceRepository(database);
        var accounts = new AccountRepository(database);
        var wavve = (await services.GetByCodeAsync("wavve"))!;

        var a = new Account { ServiceId = wavve.Id, DisplayName = "A", LoginId = "a", ProfileName = "svc_wavve_1" };
        var b = new Account { ServiceId = wavve.Id, DisplayName = "B", LoginId = "b", ProfileName = "svc_wavve_2" };
        await accounts.SaveAsync(a);
        await accounts.SaveAsync(b);

        await accounts.DeleteAsync(a.Id);

        (await accounts.GetDefaultAsync(wavve.Id))!.LoginId.Should().Be("b");
    }

    [Fact]
    public async Task Deleting_service_cascades_to_accounts()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var services = new ServiceRepository(database);
        var accounts = new AccountRepository(database);
        var custom = new StreamingService { Code = "custom_x", Name = "X", HomeUrl = "https://x.example" };
        await services.AddAsync(custom);
        await accounts.SaveAsync(new Account { ServiceId = custom.Id, DisplayName = "A", LoginId = "a", ProfileName = "svc_custom_x_1" });

        await services.DeleteAsync(custom.Id);

        (await accounts.GetByServiceAsync(custom.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Settings_store_round_trips_typed_values()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var store = new AppSettingsStore(database);

        await store.SetAsync(SettingKeys.LastServiceId, 3);
        await store.SetAsync(SettingKeys.SidebarCollapsed, true);
        await store.SetAsync(SettingKeys.WindowPlacement, new WindowPlacement(10, 20, 1600, 900, "Maximized"));
        await store.SetAsync(SettingKeys.LastServiceId, 4);

        (await store.GetAsync<int>(SettingKeys.LastServiceId)).Should().Be(4);
        (await store.GetAsync<bool>(SettingKeys.SidebarCollapsed)).Should().BeTrue();
        (await store.GetAsync<WindowPlacement>(SettingKeys.WindowPlacement)).Should().Be(new WindowPlacement(10, 20, 1600, 900, "Maximized"));
        (await store.GetAsync(SettingKeys.StartupServiceMode, SettingKeys.StartupModeLast)).Should().Be("last");

        await store.RemoveAsync(SettingKeys.LastServiceId);
        (await store.GetAsync<int?>(SettingKeys.LastServiceId)).Should().BeNull();
    }

    [Fact]
    public async Task Settings_store_applies_default_for_missing_value_types()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var store = new AppSettingsStore(database);

        (await store.GetAsync(SettingKeys.MaxAliveHosts, 3)).Should().Be(3);
        (await store.GetAsync(SettingKeys.SuspendAfterSeconds, 300)).Should().Be(300);
        (await store.GetAsync(SettingKeys.SidebarCollapsed, true)).Should().BeTrue();

        await store.SetAsync(SettingKeys.MaxAliveHosts, 0);
        (await store.GetAsync(SettingKeys.MaxAliveHosts, 3)).Should().Be(0, "저장된 0은 기본값이 아니라 실제 값이다");
    }
}
