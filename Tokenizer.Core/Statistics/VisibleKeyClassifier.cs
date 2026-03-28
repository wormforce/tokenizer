namespace Tokenizer.Core.Statistics;

public static class VisibleKeyClassifier
{
    private static readonly HashSet<int> ControlKeys =
    [
        0x08, // Backspace
        0x09, // Tab
        0x0D, // Enter
        0x10, // Shift
        0x11, // Ctrl
        0x12, // Alt
        0x13, // Pause
        0x14, // CapsLock
        0x1B, // Esc
        0x2E, // Delete
        0x5B, // LeftWin
        0x5C, // RightWin
        0x5D  // Apps
    ];

    public static bool IsCountable(int virtualKey)
    {
        if (ControlKeys.Contains(virtualKey))
        {
            return false;
        }

        if (virtualKey is >= 0x21 and <= 0x28)
        {
            return false;
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return false;
        }

        return virtualKey switch
        {
            0x20 => true,
            >= 0x30 and <= 0x39 => true,
            >= 0x41 and <= 0x5A => true,
            >= 0x60 and <= 0x6F => true,
            >= 0xBA and <= 0xC0 => true,
            >= 0xDB and <= 0xDE => true,
            _ => false
        };
    }
}

