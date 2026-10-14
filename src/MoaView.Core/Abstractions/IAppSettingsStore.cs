namespace MoaView.Core.Abstractions;

/// <summary>AppSettings Key-Value 저장소. 값은 JSON으로 직렬화한다.</summary>
public interface IAppSettingsStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
