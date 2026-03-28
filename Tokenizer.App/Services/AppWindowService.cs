using Tokenizer.App;

namespace Tokenizer.App.Services;

public sealed class AppWindowService(IAppDispatcher dispatcher)
{
    private MainWindow? _mainWindow;

    public bool IsExitRequested { get; private set; }

    public void RegisterMainWindow(MainWindow window)
    {
        _mainWindow = window;
    }

    public Task ShowMainWindowAsync()
    {
        return ShowMainWindowCoreAsync();
    }

    public Task HideMainWindowAsync()
    {
        return dispatcher.EnqueueAsync(() => _mainWindow?.HideWindow());
    }

    public Task RequestExitAsync()
    {
        return dispatcher.EnqueueAsync(() =>
        {
            IsExitRequested = true;
            _mainWindow?.Close();
        });
    }

    private async Task ShowMainWindowCoreAsync()
    {
        await ShowMainWindowAttemptAsync();

        // WinUI windows occasionally stay hidden on first show during launch.
        await Task.Delay(650);
        await ShowMainWindowAttemptAsync();

        _ = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                await Task.Delay(450);
                await ShowMainWindowAttemptAsync();
            }
        });
    }

    private Task ShowMainWindowAttemptAsync()
        => dispatcher.EnqueueAsync(() => _mainWindow?.ShowWindow());
}

