using System.IO;
using System.Text;
using MoaView.Infrastructure.Configuration;

namespace MoaView.Infrastructure.Diagnostics;

/// <summary>
/// 치명적 예외를 logs\crash-{시각}.log에 따로 남긴다(07 개발 계획 Phase 4). 일반 로그는 7일 롤링으로 사라지지만 크래시 로그는 남겨 둔다.
/// 비밀번호 같은 민감 정보는 예외 메시지에 넣지 않는 것이 원칙(NFR-05)이므로 여기서는 예외를 그대로 기록한다.
/// </summary>
public static class CrashReportWriter
{
    /// <summary>보고서 파일을 쓰고 경로를 돌려준다. 파일을 쓸 수 없으면 null(종료 중이라 예외를 내지 않는다).</summary>
    public static string? Write(AppPaths paths, Exception exception, string kind, string appVersion, string? webView2Version)
    {
        try
        {
            Directory.CreateDirectory(paths.LogsFolder);
            var file = Path.Combine(paths.LogsFolder, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(file, Build(exception, kind, appVersion, webView2Version), Encoding.UTF8);
            return file;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string Build(Exception exception, string kind, string appVersion, string? webView2Version)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"MoaView crash report ({kind})");
        sb.AppendLine($"Time      : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} (local)");
        sb.AppendLine($"App       : {appVersion}");
        sb.AppendLine($"OS        : {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})");
        sb.AppendLine($".NET      : {Environment.Version}");
        sb.AppendLine($"WebView2  : {webView2Version ?? "unknown"}");
        sb.AppendLine($"Process   : {Environment.ProcessPath}");
        sb.AppendLine($"Uptime    : {DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime:hh\\:mm\\:ss}");
        sb.AppendLine();
        sb.AppendLine(exception.ToString());

        var inner = exception.InnerException;
        var depth = 0;
        while (inner is not null && depth++ < 5)
        {
            sb.AppendLine();
            sb.AppendLine($"--- Inner[{depth}] ---");
            sb.AppendLine(inner.ToString());
            inner = inner.InnerException;
        }

        return sb.ToString();
    }
}
