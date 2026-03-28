using System.Runtime.InteropServices;
using Tokenizer.Core.Interfaces;
using Tokenizer.Infrastructure.Windows;

namespace Tokenizer.Infrastructure.Tray;

public sealed class NotifyIconTrayService : ITrayService
{
    private const int CommandOpen = 1001;
    private const int CommandTogglePause = 1002;
    private const int CommandExit = 1003;
    private const uint TrayMessage = NativeMethods.WmApp + 1;
    private const uint TrayUpdateMessage = NativeMethods.WmApp + 2;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly NativeMethods.WindowProc _windowProc;

    private Thread? _thread;
    private uint _threadId;
    private nint _windowHandle;
    private bool _isPaused;
    private int _currentCps;

    public NotifyIconTrayService()
    {
        _windowProc = WndProc;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? TogglePauseRequested;

    public event EventHandler? ExitRequested;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_thread is { IsAlive: true })
            {
                return;
            }

            _thread = new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "Tokenizer.Tray"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();

            using var registration = cancellationToken.Register(() => _readyTcs.TrySetCanceled(cancellationToken));
            await _readyTcs.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task UpdateStateAsync(bool paused, int currentCps, CancellationToken cancellationToken = default)
    {
        _isPaused = paused;
        _currentCps = currentCps;

        if (_windowHandle != 0)
        {
            NativeMethods.PostMessage(_windowHandle, TrayUpdateMessage, 0, 0);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_threadId != 0)
            {
                NativeMethods.PostThreadMessage(_threadId, NativeMethods.WmQuit, 0, 0);
            }

            _thread?.Join(2000);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private void RunMessageLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        var className = $"TokenizerTrayWindow_{Guid.NewGuid():N}";
        var wndClass = new NativeMethods.WndClassEx
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
            LpfnWndProc = Marshal.GetFunctionPointerForDelegate(_windowProc),
            HInstance = NativeMethods.GetModuleHandle(null),
            LpszClassName = className
        };

        NativeMethods.RegisterClassEx(ref wndClass);
        _windowHandle = NativeMethods.CreateWindowEx(0, className, string.Empty, 0, 0, 0, 0, 0, 0, 0, wndClass.HInstance, 0);
        if (_windowHandle == 0)
        {
            _readyTcs.TrySetException(new InvalidOperationException("Failed to create tray message window."));
            return;
        }

        AddTrayIcon();
        _readyTcs.TrySetResult();

        while (NativeMethods.GetMessage(out var message, 0, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessage(ref message);
        }

        RemoveTrayIcon();
        NativeMethods.DestroyWindow(_windowHandle);
        _windowHandle = 0;
        _threadId = 0;
    }

    private nint WndProc(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        if (msg == TrayMessage)
        {
            switch ((uint)lParam)
            {
                case NativeMethods.WmLButtonUp:
                case NativeMethods.WmLButtonDoubleClick:
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    return 0;
                case NativeMethods.WmRButtonUp:
                    ShowContextMenu();
                    return 0;
            }
        }

        if (msg == TrayUpdateMessage)
        {
            ModifyTrayIcon();
            return 0;
        }

        if (msg == NativeMethods.WmCommand)
        {
            switch ((int)(wParam & 0xFFFF))
            {
                case CommandOpen:
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    return 0;
                case CommandTogglePause:
                    TogglePauseRequested?.Invoke(this, EventArgs.Empty);
                    return 0;
                case CommandExit:
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                    return 0;
            }
        }

        if (msg == NativeMethods.WmDestroy)
        {
            RemoveTrayIcon();
            NativeMethods.PostQuitMessage(0);
            return 0;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void AddTrayIcon()
    {
        var data = BuildNotifyIconData();
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimAdd, ref data);
    }

    private void ModifyTrayIcon()
    {
        var data = BuildNotifyIconData();
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimModify, ref data);
    }

    private void RemoveTrayIcon()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        var data = BuildNotifyIconData();
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimDelete, ref data);
    }

    private NativeMethods.NotifyIconData BuildNotifyIconData()
    {
        return new NativeMethods.NotifyIconData
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            HWnd = _windowHandle,
            UId = 1,
            UFlags = NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip,
            UCallbackMessage = TrayMessage,
            HIcon = NativeMethods.LoadIcon(0, new nint(NativeMethods.IdApplication)),
            SzTip = BuildTooltipText()
        };
    }

    private string BuildTooltipText()
    {
        var status = _isPaused ? "Paused" : $"{_currentCps} c/s";
        var text = $"Tokenizer - {status}";
        return text.Length > 127 ? text[..127] : text;
    }

    private void ShowContextMenu()
    {
        if (_windowHandle == 0 || !NativeMethods.GetCursorPos(out var point))
        {
            return;
        }

        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, CommandOpen, "Open");
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, CommandTogglePause, _isPaused ? "Resume" : "Pause");
            NativeMethods.AppendMenu(menu, NativeMethods.MfSeparator, 0, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, CommandExit, "Exit");

            NativeMethods.SetForegroundWindow(_windowHandle);
            NativeMethods.TrackPopupMenu(
                menu,
                NativeMethods.TpmLeftAlign | NativeMethods.TpmBottomAlign | NativeMethods.TpmRightButton,
                point.X,
                point.Y,
                0,
                _windowHandle,
                0);
            NativeMethods.PostMessage(_windowHandle, NativeMethods.WmNull, 0, 0);
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }
}

