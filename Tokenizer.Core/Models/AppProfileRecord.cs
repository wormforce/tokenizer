namespace Tokenizer.Core.Models;

public sealed record AppProfileRecord(
    int Id,
    string ProcessName,
    string DisplayName,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc);

