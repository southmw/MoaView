using Microsoft.EntityFrameworkCore;
using MoaView.Core.Entities;

namespace MoaView.Infrastructure.Persistence;

/// <summary>
/// 기본 제공 서비스 5종(05 데이터베이스 설계 4절). 코드가 없을 때만 추가하므로 여러 번 실행해도 안전하다.
/// AllowedHosts·LoginUrlPattern은 비워 두어 Provider 기본값을 쓰게 한다. 사용자가 설정에서 바꾸면 DB 값이 우선한다.
/// 브랜드 로고는 넣지 않는다(D-08).
/// </summary>
public static class DbSeeder
{
    public static IReadOnlyList<StreamingService> BuiltInServices =>
    [
        new()
        {
            Code = "netflix", Name = "Netflix", HomeUrl = "https://www.netflix.com/browse",
            AccentColor = "#E50914", SortOrder = 1, IsBuiltIn = true, ProviderCode = "netflix",
        },
        new()
        {
            Code = "tving", Name = "TVING", HomeUrl = "https://www.tving.com",
            AccentColor = "#FF153C", SortOrder = 2, IsBuiltIn = true, ProviderCode = "tving",
        },
        new()
        {
            Code = "wavve", Name = "Wavve", HomeUrl = "https://www.wavve.com",
            AccentColor = "#1351F9", SortOrder = 3, IsBuiltIn = true, ProviderCode = "wavve",
        },
        new()
        {
            Code = "youtube", Name = "YouTube", HomeUrl = "https://www.youtube.com",
            AccentColor = "#FF0000", SortOrder = 4, IsBuiltIn = true, ProviderCode = "youtube",
        },
        new()
        {
            Code = "chzzk", Name = "치지직", HomeUrl = "https://chzzk.naver.com",
            AccentColor = "#00FFA3", SortOrder = 5, IsBuiltIn = true, ProviderCode = "chzzk",
        },
    ];

    /// <summary>추가된 서비스 수를 돌려준다.</summary>
    public static async Task<int> SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.Services.Select(s => s.Code).ToListAsync(cancellationToken);
        var missing = BuiltInServices.Where(s => !existing.Contains(s.Code)).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        db.Services.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }
}
