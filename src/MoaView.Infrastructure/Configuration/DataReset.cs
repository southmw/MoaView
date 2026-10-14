using System.IO;

namespace MoaView.Infrastructure.Configuration;

/// <summary>
/// 모든 데이터 초기화(D-18): 설정·서비스·계정 DB, 서비스별 브라우저 데이터, 파비콘 캐시를 지워 처음 상태로 돌린다.
/// 실행 중에는 DB와 WebView2 폴더가 잠겨 있으므로 표식 파일만 만들고, 다음 시작 때 DB를 열기 전에 수행한다. 로그는 남긴다.
/// </summary>
public static class DataReset
{
    public static bool IsScheduled(AppPaths paths) => File.Exists(paths.ResetMarkerFile);

    public static void Schedule(AppPaths paths)
    {
        Directory.CreateDirectory(paths.Root);
        File.WriteAllText(paths.ResetMarkerFile, DateTime.UtcNow.ToString("O"));
    }

    public static void Cancel(AppPaths paths) => File.Delete(paths.ResetMarkerFile);

    /// <summary>
    /// 표식이 있으면 초기화를 수행하고 지운 항목을 돌려준다. 잠긴 폴더는 몇 번 재시도하고, 그래도 실패하면 errors에 담고 표식은 남겨 다음 시작 때 다시 시도한다.
    /// 표식이 없으면 빈 목록.
    /// </summary>
    public static IReadOnlyList<string> ExecuteIfScheduled(AppPaths paths, out IReadOnlyList<string> errors, int attempts = 10, int retryDelayMs = 500)
    {
        var removed = new List<string>();
        var failed = new List<string>();
        errors = failed;
        if (!IsScheduled(paths))
        {
            return removed;
        }

        foreach (var file in new[] { paths.DatabaseFile, paths.DatabaseFile + "-wal", paths.DatabaseFile + "-shm", paths.DatabaseFile + "-journal" })
        {
            Try(() => File.Delete(file), file, exists: File.Exists(file));
        }

        foreach (var folder in new[] { paths.WebView2UserDataFolder, paths.IconsFolder })
        {
            Try(() => Directory.Delete(folder, recursive: true), folder, exists: Directory.Exists(folder));
        }

        if (failed.Count == 0)
        {
            File.Delete(paths.ResetMarkerFile);
        }

        return removed;

        void Try(Action action, string target, bool exists)
        {
            if (!exists)
            {
                return;
            }

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    action();
                    removed.Add(target);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (attempt == attempts)
                    {
                        failed.Add($"{target}: {ex.Message}");
                        return;
                    }

                    Thread.Sleep(retryDelayMs);
                }
            }
        }
    }
}
