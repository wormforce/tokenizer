namespace Tokenizer.Core.Models;

public sealed record KeyStrokeSample(
    DateTimeOffset CapturedAtUtc,
    int VirtualKey,
    int ScanCode,
    bool IsInjected,
    nint WindowHandle,
    uint ProcessId);

