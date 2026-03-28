using Tokenizer.Core.Interfaces;
using Windows.ApplicationModel;

namespace Tokenizer.Infrastructure.Autostart;

public sealed class StartupTaskAutostartService : IAutostartService
{
    private const string StartupTaskId = "TokenizerStartupTask";

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId).AsTask(cancellationToken);
            return startupTask.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId).AsTask(cancellationToken);
            if (enabled)
            {
                var state = await startupTask.RequestEnableAsync().AsTask(cancellationToken);
                return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            }

            startupTask.Disable();
            return startupTask.State is StartupTaskState.Disabled or StartupTaskState.DisabledByUser;
        }
        catch
        {
            return false;
        }
    }
}

