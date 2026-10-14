using System.Threading;

namespace MoaView.App.Services;

/// <summary>
/// 07 개발 계획 Phase 4: 중복 실행 방지. 이름 있는 Mutex로 첫 인스턴스를 가리고, 두 번째 인스턴스는 이름 있는 이벤트를 울려
/// 첫 인스턴스가 창을 앞으로 가져오게 한 뒤 종료한다. "Local\" 접두사라 같은 로그인 세션 안에서만 하나다.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\MoaView.SingleInstance";
    private const string ActivateEventName = @"Local\MoaView.Activate";

    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _registration;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }

    public bool IsFirstInstance { get; }

    /// <summary>두 번째 인스턴스가 호출: 먼저 뜬 인스턴스에 "창을 보여 달라"고 알린다.</summary>
    public void SignalExistingInstance()
    {
        using var handle = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        handle.Set();
    }

    /// <summary>첫 인스턴스가 호출: 신호가 올 때마다 onActivate를 스레드 풀에서 부른다(호출자가 Dispatcher로 넘긴다).</summary>
    public void ListenForActivation(Action onActivate)
    {
        if (!IsFirstInstance || _activateEvent is not null)
        {
            return;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _registration = null;
        _activateEvent?.Dispose();
        _activateEvent = null;

        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 다른 스레드가 소유한 경우. 프로세스 종료로 어차피 풀린다.
            }

            _mutex.Dispose();
            _mutex = null;
        }
    }
}
