using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MoaView.Core.Entities;

namespace MoaView.Infrastructure.Persistence;

/// <summary>
/// SQLite 스키마(05 데이터베이스 설계). 시각은 UTC, 목록 컬럼은 JSON 문자열로 저장한다.
/// 긴 수명의 ViewModel에서는 IDbContextFactory로 짧게 열고 닫는다(05 문서 6절).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StreamingService> Services => Set<StreamingService>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AppSetting> Settings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListConverter = new ValueConverter<List<string>, string>(
            list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
            json => string.IsNullOrWhiteSpace(json)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            list => list.ToList());

        modelBuilder.Entity<StreamingService>(entity =>
        {
            entity.ToTable("StreamingServices");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Code).IsRequired().HasMaxLength(64);
            entity.HasIndex(s => s.Code).IsUnique();
            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
            entity.Property(s => s.HomeUrl).IsRequired().HasMaxLength(2048);
            entity.Property(s => s.LoginUrl).HasMaxLength(2048);
            entity.Property(s => s.LoginUrlPattern).HasMaxLength(512);
            entity.Property(s => s.AllowedHosts)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
            entity.Property(s => s.IconPath).HasMaxLength(1024);
            entity.Property(s => s.AccentColor).HasMaxLength(16);
            entity.Property(s => s.ProviderCode).IsRequired().HasMaxLength(32).HasDefaultValue("generic");
            entity.Property(s => s.UserAgentOverride).HasMaxLength(512);
            entity.Property(s => s.ZoomFactor).HasDefaultValue(1.0);
            entity.Property(s => s.PauseOnSwitch).HasDefaultValue(true);
            entity.Property(s => s.IsEnabled).HasDefaultValue(true);
            entity.Property(s => s.LastUrl).HasMaxLength(4096);
            entity.Ignore(s => s.ProfileName);

            entity.HasMany(s => s.Accounts)
                .WithOne(a => a.Service)
                .HasForeignKey(a => a.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.DisplayName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.LoginId).IsRequired().HasMaxLength(256);
            entity.Property(a => a.LoginProvider).IsRequired().HasMaxLength(32).HasDefaultValue(LoginProviders.Native);
            entity.Property(a => a.ProfileName).IsRequired().HasMaxLength(128);
            entity.Property(a => a.AutoFillEnabled).HasDefaultValue(true);
            entity.Property(a => a.AutoSubmitEnabled).HasDefaultValue(false);

            // 같은 컬럼에 인덱스를 둘 두려면 모델 이름을 각각 줘야 한다(이름 없이 두 번 부르면 하나로 합쳐진다).
            entity.HasIndex(a => a.ServiceId, "IX_Accounts_ServiceId");
            entity.HasIndex(a => a.ProfileName, "UX_Accounts_ProfileName").IsUnique();

            // 서비스 하나에 기본 계정은 최대 1개(Partial Unique Index).
            entity.HasIndex(a => a.ServiceId, "UX_Accounts_ServiceId_Default")
                .IsUnique()
                .HasFilter("\"IsDefault\" = 1");
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("AppSettings");
            entity.HasKey(s => s.Key);
            entity.Property(s => s.Key).HasMaxLength(128);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    private void StampTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    break;
            }
        }
    }
}
