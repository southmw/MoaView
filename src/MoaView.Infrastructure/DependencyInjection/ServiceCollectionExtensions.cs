using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoaView.Core.Abstractions;
using MoaView.Infrastructure.Configuration;
using MoaView.Infrastructure.Persistence;
using MoaView.Infrastructure.Persistence.Repositories;
using MoaView.Infrastructure.Providers;
using MoaView.Infrastructure.Security;
using MoaView.Infrastructure.WebView;

namespace MoaView.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton<IWebView2EnvironmentProvider, WebView2EnvironmentProvider>();

        // 05 데이터베이스 설계 6절: 긴 수명의 ViewModel에서 컨텍스트 수명 문제를 피하기 위해 팩터리로 주입한다.
        // Cache=Shared는 쓰지 않는다. SQLite 문서가 공유 캐시 모드를 권하지 않고, 짧은 연결을 여러 개 여는 구조에서는 이점이 없다.
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabaseFile}"));
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IDatabaseMaintenance, DatabaseMaintenance>();
        services.AddSingleton<ServiceRepository>();
        services.AddSingleton<IServiceRepository>(sp => sp.GetRequiredService<ServiceRepository>());
        services.AddSingleton<IServiceCatalog>(sp => sp.GetRequiredService<ServiceRepository>());
        services.AddSingleton<IAccountRepository, AccountRepository>();
        services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        services.AddSingleton<ICredentialProtector, DpapiCredentialProtector>();

        services.AddSingleton<IStreamingProvider, NetflixProvider>();
        services.AddSingleton<IStreamingProvider, TvingProvider>();
        services.AddSingleton<IStreamingProvider, WavveProvider>();
        services.AddSingleton<IStreamingProvider, YouTubeProvider>();
        services.AddSingleton<IStreamingProvider, ChzzkProvider>();
        services.AddSingleton<IStreamingProvider, GenericProvider>();
        services.AddSingleton<IStreamingProviderResolver, StreamingProviderResolver>();

        return services;
    }
}
