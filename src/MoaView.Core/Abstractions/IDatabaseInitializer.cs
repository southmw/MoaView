namespace MoaView.Core.Abstractions;

/// <summary>앱 시작 시 마이그레이션 적용과 기본 서비스 Seed(04 아키텍처 5.1절).</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
