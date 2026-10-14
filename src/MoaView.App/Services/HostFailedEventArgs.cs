using MoaView.Core.Entities;

namespace MoaView.App.Services;

/// <summary>서비스 WebView에서 생긴 문제를 배너로 알리기 위한 정보(06 UI/UX 5절 상태별 화면).</summary>
public sealed class HostFailedEventArgs(StreamingService service, string title, string message, bool canRetry) : EventArgs
{
    public StreamingService Service { get; } = service;
    public string Title { get; } = title;
    public string Message { get; } = message;
    public bool CanRetry { get; } = canRetry;
}
