using System.Diagnostics;
using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;

namespace Tokenizer.Infrastructure.Windows;

public sealed class ForegroundAppService : IForegroundAppService
{
    public Task<ForegroundAppInfo?> ResolveAsync(KeyStrokeSample sample, CancellationToken cancellationToken = default)
    {
        try
        {
            var processId = sample.ProcessId;
            if (processId == 0 && sample.WindowHandle != nint.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(sample.WindowHandle, out processId);
            }

            if (processId == 0)
            {
                return Task.FromResult<ForegroundAppInfo?>(null);
            }

            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;
            var displayName = ResolveDisplayName(process) ?? processName;

            return Task.FromResult<ForegroundAppInfo?>(new ForegroundAppInfo(processId, processName, displayName));
        }
        catch
        {
            return Task.FromResult<ForegroundAppInfo?>(null);
        }
    }

    private static string? ResolveDisplayName(Process process)
    {
        try
        {
            var fileName = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var description = FileVersionInfo.GetVersionInfo(fileName).FileDescription;
            return string.IsNullOrWhiteSpace(description) ? null : description;
        }
        catch
        {
            return null;
        }
    }
}

