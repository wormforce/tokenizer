namespace Tokenizer.Core.Models;

public sealed record ForegroundAppInfo(
    uint ProcessId,
    string ProcessName,
    string DisplayName);

