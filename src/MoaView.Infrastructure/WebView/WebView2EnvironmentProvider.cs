using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using MoaView.Infrastructure.Configuration;

namespace MoaView.Infrastructure.WebView;

public sealed class WebView2EnvironmentProvider(AppPaths paths, ILogger<WebView2EnvironmentProvider> logger)
    : IWebView2EnvironmentProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CoreWebView2Environment? _environment;

    public string? GetRuntimeVersion()
    {
        try
        {
            return CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return null;
        }
    }

    public async Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        if (_environment is not null)
        {
            return _environment;
        }

        await _gate.WaitAsync();
        try
        {
            if (_environment is null)
            {
                paths.EnsureCreated();

                // 브라우저 인자는 기본값을 유지한다(--disable-gpu 금지, 03 기술 검토 4.3절).
                // 확장 허용은 치지직 그리드(P5-b)를 위해 켠다. 확장을 로드하지 않으면 영향이 없다.
                var options = new CoreWebView2EnvironmentOptions
                {
                    AreBrowserExtensionsEnabled = true,
                };

                _environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: paths.WebView2UserDataFolder,
                    options: options);

                logger.LogInformation("WebView2 환경 생성. 런타임 {Version}, 데이터 폴더 {Folder}",
                    _environment.BrowserVersionString, paths.WebView2UserDataFolder);
            }

            return _environment;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CoreWebView2ControllerOptions> CreateControllerOptionsAsync(string profileName)
    {
        var environment = await GetEnvironmentAsync();
        var options = environment.CreateCoreWebView2ControllerOptions();
        options.ProfileName = profileName;
        options.IsInPrivateModeEnabled = false;
        return options;
    }

    public IReadOnlyList<CoreWebView2ProcessInfo> GetProcessInfos() =>
        _environment?.GetProcessInfos() ?? [];
}
