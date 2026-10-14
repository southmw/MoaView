using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace MoaView.App.Services;

/// <summary>
/// D-17: GitHub Releases를 업데이트 채널로 쓰는 Velopack 자동 업데이트. 설치된 앱(Velopack 설치기로 설치)에서만 동작하고,
/// 개발 실행이나 포터블 복사본에서는 IsInstalled가 false라 아무것도 하지 않는다.
/// </summary>
public sealed class UpdateService(ILogger<UpdateService> logger)
{
    public const string RepositoryUrl = "https://github.com/southmw/MoaView";

    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
    private UpdateInfo? _pending;

    /// <summary>Velopack으로 설치된 앱인지. 아니면 업데이트 확인을 건너뛴다.</summary>
    public bool IsInstalled
    {
        get
        {
            try
            {
                return _manager.IsInstalled;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Velopack 설치 상태 확인 실패");
                return false;
            }
        }
    }

    public string? CurrentVersion => IsInstalled ? _manager.CurrentVersion?.ToString() : null;

    /// <summary>다운로드까지 끝나 다시 시작하면 적용되는 버전. 없으면 null.</summary>
    public string? ReadyVersion { get; private set; }

    /// <summary>새 버전이 있으면 버전 문자열을, 없거나 확인할 수 없으면 null을 돌려준다.</summary>
    public async Task<string?> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInstalled)
        {
            return null;
        }

        try
        {
            _pending = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
            var version = _pending?.TargetFullRelease.Version.ToString();
            logger.LogInformation("업데이트 확인: 현재 {Current}, 최신 {Latest}", CurrentVersion, version ?? "(없음)");
            return version;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "업데이트 확인 실패");
            return null;
        }
    }

    /// <summary>CheckAsync가 찾은 버전을 내려받는다. 성공하면 ReadyVersion이 채워진다.</summary>
    public async Task<bool> DownloadAsync(Action<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_pending is null)
        {
            return false;
        }

        try
        {
            await _manager.DownloadUpdatesAsync(_pending, progress, cancellationToken);
            ReadyVersion = _pending.TargetFullRelease.Version.ToString();
            logger.LogInformation("업데이트 다운로드 완료: {Version}", ReadyVersion);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "업데이트 다운로드 실패");
            return false;
        }
    }

    /// <summary>내려받은 업데이트를 적용하고 앱을 다시 시작한다. 이 호출은 돌아오지 않는다.</summary>
    public void ApplyAndRestart()
    {
        if (_pending is null)
        {
            return;
        }

        logger.LogInformation("업데이트 적용 후 다시 시작: {Version}", ReadyVersion);
        _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
    }
}
