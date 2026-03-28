namespace Tokenizer.Core.Models;

public sealed record AppRankingItem(
    int AppId,
    string DisplayName,
    string ProcessName,
    int CharCount,
    int PeakCps,
    int ActiveSeconds);

