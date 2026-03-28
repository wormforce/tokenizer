namespace Tokenizer.Core.Interfaces;

public interface IRealtimeSpeedCalculator
{
    int Register(DateTimeOffset capturedAtUtc);

    int Refresh(DateTimeOffset now);

    int CurrentCps { get; }

    void Reset();
}

