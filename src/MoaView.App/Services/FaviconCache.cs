using System.IO;
using Microsoft.Extensions.Logging;
using MoaView.Infrastructure.Configuration;

namespace MoaView.App.Services;

/// <summary>
/// FR-SVC-06 / D-08: 브랜드 로고를 앱에 넣는 대신, 실행 중에 페이지가 쓰는 파비콘을 받아 %LOCALAPPDATA%\MoaView\icons\{code}.png에 캐시한다.
/// 파일이 없거나 오래됐을 때만 다시 받는다. 없으면 사이드바는 이니셜 아이콘을 쓴다.
/// </summary>
public sealed class FaviconCache(AppPaths paths, ILogger<FaviconCache> logger)
{
    private static readonly TimeSpan RefreshAfter = TimeSpan.FromDays(7);

    public string PathFor(string serviceCode) => Path.Combine(paths.IconsFolder, serviceCode + ".png");

    /// <summary>캐시가 없거나 7일이 지났으면 true.</summary>
    public bool NeedsRefresh(string serviceCode)
    {
        var file = PathFor(serviceCode);
        return !File.Exists(file) || DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > RefreshAfter;
    }

    /// <summary>PNG 바이트를 저장하고 절대 경로를 돌려준다. 실패하면 null.</summary>
    public string? Save(string serviceCode, byte[] png)
    {
        if (png.Length == 0)
        {
            return null;
        }

        var file = PathFor(serviceCode);
        try
        {
            Directory.CreateDirectory(paths.IconsFolder);
            // 사이드바가 이전 파일을 읽는 중일 수 있어 임시 파일에 쓴 뒤 바꿔치기한다.
            var temp = file + ".tmp";
            File.WriteAllBytes(temp, png);
            File.Move(temp, file, overwrite: true);
            logger.LogInformation("[{Service}] 파비콘 캐시 저장 ({Bytes} bytes)", serviceCode, png.Length);
            return file;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "[{Service}] 파비콘 캐시 저장 실패", serviceCode);
            return null;
        }
    }

    public void Delete(string serviceCode)
    {
        try
        {
            File.Delete(PathFor(serviceCode));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "[{Service}] 파비콘 캐시 삭제 실패", serviceCode);
        }
    }
}
