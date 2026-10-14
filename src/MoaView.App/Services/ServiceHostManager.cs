using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoaView.App.Controls;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;
using MoaView.Core.Models;
using MoaView.Infrastructure.Configuration;

namespace MoaView.App.Services;

public sealed class ServiceHostManager : IServiceHostManager
{
    private static readonly TimeSpan LastUrlSaveDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ZoomSaveDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromSeconds(30);

    private readonly ServiceHostFactory _factory;
    private readonly IServiceRepository _services;
    private readonly IAppSettingsStore _settings;
    private readonly FaviconCache _favicons;
    private readonly IOptions<PlayerOptions> _options;
    private readonly ILogger<ServiceHostManager> _logger;

    private readonly Dictionary<string, ServiceHostControl> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _pendingSaves = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _maintenance;
    private Panel? _hostArea;
    private bool _maintaining;

    public ServiceHostManager(
        ServiceHostFactory factory,
        IServiceRepository services,
        IAppSettingsStore settings,
        FaviconCache favicons,
        IOptions<PlayerOptions> options,
        ILogger<ServiceHostManager> logger)
    {
        _factory = factory;
        _services = services;
        _settings = settings;
        _favicons = favicons;
        _options = options;
        _logger = logger;

        _maintenance = new DispatcherTimer(DispatcherPriority.Background) { Interval = MaintenanceInterval };
        _maintenance.Tick += async (_, _) => await MaintainAsync();
    }

    public ServiceHostControl? Active { get; private set; }

    public ServiceHostControl? GetHost(string serviceCode) => _hosts.GetValueOrDefault(serviceCode);

    public event EventHandler<ServiceHostControl>? HostCreated;
    public event EventHandler<ServiceHostControl>? HostDisposed;
    public event EventHandler<bool>? FullScreenElementChanged;
    public event EventHandler? ActiveStateChanged;
    public event EventHandler<HostFailedEventArgs>? HostFailed;
    public event EventHandler<ServiceHostControl>? ServiceIconChanged;

    public void Attach(Panel hostArea)
    {
        _hostArea = hostArea;
        _maintenance.Start();
    }

    public async Task ActivateAsync(StreamingService service)
    {
        if (_hostArea is null)
        {
            throw new InvalidOperationException("Attach(hostArea)를 먼저 호출해야 합니다.");
        }

        if (Active?.Service.Code == service.Code)
        {
            return;
        }

        var previous = Active;
        if (previous is not null)
        {
            previous.LastActiveAt = DateTime.UtcNow;
        }

        if (!_hosts.TryGetValue(service.Code, out var host))
        {
            host = _factory.Create(service);
            host.FullScreenElementChanged += OnHostFullScreenElementChanged;
            host.StateChanged += OnHostStateChanged;
            host.ZoomChanged += OnHostZoomChanged;
            host.Failed += OnHostFailed;
            host.FaviconChanged += OnHostFaviconChanged;
            _hosts[service.Code] = host;
            HostCreated?.Invoke(this, host);
            _hostArea.Children.Add(host);

            // 새 호스트를 먼저 보이게 한 뒤 초기화한다(R-20). 이전 호스트는 그다음에 숨겨서 깜빡임을 줄인다.
            host.Visibility = Visibility.Visible;
            Active = host;
            HidePrevious(previous);
            ActiveStateChanged?.Invoke(this, EventArgs.Empty);

            await host.InitializeAsync();
        }
        else
        {
            host.Visibility = Visibility.Visible;
            host.Resume();
            Active = host;
            HidePrevious(previous);
            ActiveStateChanged?.Invoke(this, EventArgs.Empty);
        }

        host.LastActiveAt = DateTime.UtcNow;

        if (previous is not null && _options.Value.PauseOnSwitch && previous.Service.PauseOnSwitch)
        {
            await previous.PauseMediaAsync();
        }

        _logger.LogInformation("서비스 전환: {Service}", service.Code);
        await EnforceMaxAliveAsync();
    }

    public void RemoveHost(string serviceCode)
    {
        if (!_hosts.TryGetValue(serviceCode, out var host))
        {
            return;
        }

        if (Active == host)
        {
            Active = null;
            ActiveStateChanged?.Invoke(this, EventArgs.Empty);
        }

        _logger.LogInformation("서비스 호스트 해제(설정 변경): {Service}", serviceCode);
        DisposeHost(host);
    }

    public void GoBack() => Active?.GoBack();

    public void GoForward() => Active?.GoForward();

    public void Reload() => Active?.Reload();

    public void GoHome() => Active?.GoHome();

    public void GoHome(string serviceCode) => GetHost(serviceCode)?.GoHome();

    public void Reload(string serviceCode) => GetHost(serviceCode)?.Reload();

    public Task ClearSessionAsync(string serviceCode) =>
        GetHost(serviceCode)?.ClearSessionAsync() ?? Task.CompletedTask;

    public Task ExitVideoFullScreenAsync() => Active?.ExitVideoFullScreenAsync() ?? Task.CompletedTask;

    public Task TogglePictureInPictureAsync() => Active?.TogglePictureInPictureAsync() ?? Task.CompletedTask;

    public void ZoomIn() => Active?.ZoomIn();

    public void ZoomOut() => Active?.ZoomOut();

    public void ResetZoom() => Active?.ResetZoom();

    // ---------------------------------------------------------------- Suspend / LRU (FR-PLY-05)

    private async Task MaintainAsync()
    {
        if (_maintaining)
        {
            return;
        }

        _maintaining = true;
        try
        {
            var suspendAfter = TimeSpan.FromSeconds(
                await _settings.GetAsync(SettingKeys.SuspendAfterSeconds, _options.Value.SuspendAfterSeconds));
            if (suspendAfter <= TimeSpan.Zero)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var host in _hosts.Values.Where(h => h != Active && !h.IsSuspended && h.IsReady).ToList())
            {
                if (now - host.LastActiveAt >= suspendAfter)
                {
                    await host.TrySuspendAsync();
                }
            }

            await EnforceMaxAliveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "호스트 유지 관리 실패");
        }
        finally
        {
            _maintaining = false;
        }
    }

    /// <summary>최대 유지 개수를 넘으면 가장 오래 안 쓴 비활성 호스트부터 해제한다. 마지막 URL은 DB에 있어 다시 열면 이어진다.</summary>
    private async Task EnforceMaxAliveAsync()
    {
        var maxAlive = Math.Max(1, await _settings.GetAsync(SettingKeys.MaxAliveHosts, _options.Value.MaxAliveHosts));
        while (_hosts.Count > maxAlive)
        {
            var victim = _hosts.Values
                .Where(h => h != Active)
                .OrderBy(h => h.LastActiveAt)
                .FirstOrDefault();
            if (victim is null)
            {
                return;
            }

            DisposeHost(victim);
        }
    }

    private void DisposeHost(ServiceHostControl host)
    {
        host.FullScreenElementChanged -= OnHostFullScreenElementChanged;
        host.StateChanged -= OnHostStateChanged;
        host.ZoomChanged -= OnHostZoomChanged;
        host.Failed -= OnHostFailed;
        host.FaviconChanged -= OnHostFaviconChanged;
        _hosts.Remove(host.Service.Code);
        _hostArea?.Children.Remove(host);
        host.Dispose();
        HostDisposed?.Invoke(this, host);
    }

    // ---------------------------------------------------------------- 이벤트

    private static void HidePrevious(ServiceHostControl? previous)
    {
        if (previous is not null)
        {
            previous.Visibility = Visibility.Collapsed;
        }
    }

    private void OnHostFullScreenElementChanged(object? sender, bool isFullScreen)
    {
        if (sender == Active)
        {
            FullScreenElementChanged?.Invoke(this, isFullScreen);
        }
    }

    private void OnHostStateChanged(object? sender, EventArgs e)
    {
        if (sender is ServiceHostControl host)
        {
            SchedulePersist("url:" + host.Service.Code, LastUrlSaveDelay, async () =>
            {
                // 로그인·OAuth 페이지(토큰 포함 가능)나 서비스 밖 URL은 저장하지 않는다. 이전 값을 유지한다.
                var url = host.PersistableUrl;
                if (url is null)
                {
                    return;
                }

                await _services.UpdateLastUrlAsync(host.Service.Id, url);
                host.Service.LastUrl = url;
                _logger.LogDebug("[{Service}] 마지막 URL 저장: {Url}", host.Service.Code, ServiceHostControl.SanitizeForLog(url));
            });
        }

        if (sender == Active)
        {
            ActiveStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnHostZoomChanged(object? sender, double zoom)
    {
        if (sender is ServiceHostControl host)
        {
            SchedulePersist("zoom:" + host.Service.Code, ZoomSaveDelay,
                () => _services.UpdateZoomFactorAsync(host.Service.Id, zoom));
        }
    }

    /// <summary>FR-SVC-06: 캐시가 없거나 오래됐을 때만 파비콘을 받아 저장하고 DB·사이드바에 반영한다.</summary>
    private async void OnHostFaviconChanged(object? sender, EventArgs e)
    {
        if (sender is not ServiceHostControl host || !_favicons.NeedsRefresh(host.Service.Code))
        {
            return;
        }

        try
        {
            var png = await host.GetFaviconPngAsync();
            if (png is null || host.IsDisposed)
            {
                return;
            }

            var path = _favicons.Save(host.Service.Code, png);
            if (path is null)
            {
                return;
            }

            await _services.UpdateIconPathAsync(host.Service.Id, path);
            host.Service.IconPath = path;
            ServiceIconChanged?.Invoke(this, host);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Service}] 파비콘 캐시 실패", host.Service.Code);
        }
    }

    private void OnHostFailed(object? sender, HostFailedEventArgs e)
    {
        if (sender == Active)
        {
            HostFailed?.Invoke(this, e);
        }
    }

    /// <summary>같은 키의 저장 요청이 잇달아 오면 마지막 것만 지연 실행한다(디바운스).</summary>
    private void SchedulePersist(string key, TimeSpan delay, Func<Task> action)
    {
        if (_pendingSaves.Remove(key, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        var cts = new CancellationTokenSource();
        _pendingSaves[key] = cts;
        _ = RunLaterAsync(cts.Token);

        async Task RunLaterAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(delay, token);
                await action();
            }
            catch (OperationCanceledException)
            {
                // 더 새로운 요청으로 대체됨
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "저장 실패: {Key}", key);
            }
        }
    }
}
