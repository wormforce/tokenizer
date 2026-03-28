using Microsoft.Win32;
using Tokenizer.Core.Interfaces;

namespace Tokenizer.Infrastructure.Autostart;

public sealed class StartupTaskAutostartService : IAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Tokenizer";

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();

        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var configuredCommand = runKey?.GetValue(RunValueName) as string;
        if (string.IsNullOrWhiteSpace(configuredCommand))
        {
            return false;
        }

        return string.Equals(configuredCommand, BuildAutostartCommand(), StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (runKey is null)
        {
            return false;
        }

        if (enabled)
        {
            runKey.SetValue(RunValueName, BuildAutostartCommand(), RegistryValueKind.String);
            return await IsEnabledAsync(cancellationToken);
        }

        runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
        return !await IsEnabledAsync(cancellationToken);
    }

    private static string BuildAutostartCommand()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Unable to resolve the current executable path for autostart.");
        }

        return $"\"{processPath}\"";
    }
}
