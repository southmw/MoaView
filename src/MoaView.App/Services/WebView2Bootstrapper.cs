using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace MoaView.App.Services;

/// <summary>
/// 07 개발 계획 Phase 4: WebView2 런타임이 없을 때 Microsoft Evergreen 부트스트래퍼(약 2 MB)를 받아 실행한다.
/// 부트스트래퍼가 실제 런타임을 내려받아 설치하며, 설치는 관리자 권한을 요구할 수 있다(UAC). Velopack 설치기로 설치한 경우에는
/// 설치 단계에서 이미 런타임을 넣으므로 이 경로는 포터블·개발 실행에서만 쓰인다.
/// </summary>
public static class WebView2Bootstrapper
{
    /// <summary>Microsoft가 안내하는 Evergreen Bootstrapper 고정 링크(x86/x64/ARM64 공통).</summary>
    private const string BootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public const string DownloadPageUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    /// <summary>부트스트래퍼를 받아 설치를 실행하고 끝날 때까지 기다린다. 성공하면 true. 네트워크·실행 실패는 로그로 남기고 false.</summary>
    public static async Task<bool> TryInstallAsync(ILogger logger, CancellationToken cancellationToken = default)
    {
        var file = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            await using (var stream = await http.GetStreamAsync(BootstrapperUrl, cancellationToken))
            await using (var target = File.Create(file))
            {
                await stream.CopyToAsync(target, cancellationToken);
            }

            logger.LogInformation("WebView2 부트스트래퍼 다운로드 완료: {File}", file);

            using var process = Process.Start(new ProcessStartInfo(file, "/install")
            {
                UseShellExecute = true,
            });
            if (process is null)
            {
                logger.LogWarning("WebView2 부트스트래퍼를 시작하지 못했습니다.");
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            logger.LogInformation("WebView2 부트스트래퍼 종료 코드 {Code}", process.ExitCode);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(ex, "WebView2 런타임 설치 실패");
            return false;
        }
        finally
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // 설치기가 아직 잡고 있으면 다음 임시 파일 정리에 맡긴다.
            }
        }
    }
}
