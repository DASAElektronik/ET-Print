namespace ETPrinter.Tests;

/// <summary>Fuehrt eine Aktion auf einem STA-Thread aus — WPF-Elemente
/// (FixedDocument, TextBlock, ...) verlangen STA, xUnit laeuft auf MTA.</summary>
public static class Sta
{
    public static void Run(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    public static T Run<T>(Func<T> func)
    {
        T result = default!;
        Run(() => { result = func(); });
        return result;
    }

    /// <summary>Async-Ablauf auf einem STA-Thread mit Dispatcher-Schleife: Fortsetzungen
    /// nach await landen wieder auf dem STA-Thread (wie in der WPF-Anwendung).</summary>
    public static Task RunAsync(Func<Task> func)
    {
        var tcs = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
                var frame = new System.Windows.Threading.DispatcherFrame();
                var task = func();
                task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                if (task.IsFaulted) tcs.SetException(task.Exception!.InnerExceptions);
                else tcs.SetResult();
                dispatcher.InvokeShutdown();
            }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
