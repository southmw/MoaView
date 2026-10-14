using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MoaView.Infrastructure.Persistence;

/// <summary>
/// `dotnet ef migrations add` 전용. 실제 DB 경로와 무관한 임시 연결 문자열을 쓴다.
/// 사용법: dotnet ef migrations add &lt;Name&gt; --project src/MoaView.Infrastructure --startup-project src/MoaView.App --output-dir Persistence/Migrations
/// (시작 프로젝트가 App이어야 Windows SDK 어셈블리를 찾는다)
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new AppDbContext(options);
    }
}
