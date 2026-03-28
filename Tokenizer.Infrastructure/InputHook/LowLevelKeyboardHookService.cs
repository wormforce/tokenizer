using System.Runtime.InteropServices;
using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;
using Tokenizer.Infrastructure.Windows;

namespace Tokenizer.Infrastructure.InputHook;

public sealed class LowLevelKeyboardHookService : IKeyboardHookService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TaskCompletionSource _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private NativeMethods.HookProc? _hookProc;
    private Thread? _hookThread;
    private nint _hookHandle;
    private uint _threadId;

    public event EventHandler<KeyStrokeSample>? KeyCaptured;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_hookThread is { IsAlive: true })
            {
                return;
            }

            _hookProc = HookCallback;
            _hookThread = new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "Tokenizer.KeyboardHook"
            };
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.Start();

            using var registration = cancellationToken.Register(() => _startedTcs.TrySetCanceled(cancellationToken));
            await _startedTcs.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_threadId != 0)
            {
                NativeMethods.PostThreadMessage(_threadId, NativeMethods.WmQuit, 0, 0);
            }

            if (_hookThread is not null)
            {
                await Task.Run(() => _hookThread.Join(2000), cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        catch
        {
            // Ignore shutdown races.
        }

        _gate.Dispose();
    }

    private void RunMessageLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();

        var module = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _hookProc!, module, 0);
        if (_hookHandle == 0)
        {
            _startedTcs.TrySetException(new InvalidOperationException("Failed to install low-level keyboard hook."));
            return;
        }

        _startedTcs.TrySetResult();

        while (NativeMethods.GetMessage(out var message, 0, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessage(ref message);
        }

        if (_hookHandle != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }

        _threadId = 0;
    }

    private nint HookCallback(int nCode, nuint wParam, nint lParam)
    {
        if (nCode >= 0 && (wParam == NativeMethods.WmKeyDown || wParam == NativeMethods.WmSysKeyDown))
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            var windowHandle = NativeMethods.GetForegroundWindow();
            NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);

            KeyCaptured?.Invoke(this, new KeyStrokeSample(
                DateTimeOffset.UtcNow,
                unchecked((int)hookStruct.VkCode),
                unchecked((int)hookStruct.ScanCode),
                (hookStruct.Flags & NativeMethods.LlkhfInjected) != 0,
                windowHandle,
                processId));
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}

