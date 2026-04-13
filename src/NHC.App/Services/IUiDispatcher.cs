using Microsoft.UI.Dispatching;

namespace NHC.App.Services;

/// <summary>
/// Marshals work onto the UI thread. Wrapping <see cref="DispatcherQueue"/>
/// behind an interface lets view-models stay unit-testable without
/// depending on WinUI directly.
/// </summary>
public interface IUiDispatcher
{
    void Enqueue(Action action);
}

public sealed class DispatcherQueueAdapter : IUiDispatcher
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    public void Enqueue(Action action)
    {
        if (_queue.HasThreadAccess) action();
        else _queue.TryEnqueue(() => action());
    }
}
