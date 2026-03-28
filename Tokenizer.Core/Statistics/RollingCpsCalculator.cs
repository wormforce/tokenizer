using Tokenizer.Core.Interfaces;

namespace Tokenizer.Core.Statistics;

public sealed class RollingCpsCalculator : IRealtimeSpeedCalculator
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(2);
    private readonly Queue<DateTimeOffset> _samples = new();
    private readonly object _gate = new();

    public int CurrentCps { get; private set; }

    public int Register(DateTimeOffset capturedAtUtc)
    {
        lock (_gate)
        {
            _samples.Enqueue(capturedAtUtc);
            return RefreshCore(capturedAtUtc);
        }
    }

    public int Refresh(DateTimeOffset now)
    {
        lock (_gate)
        {
            return RefreshCore(now);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _samples.Clear();
            CurrentCps = 0;
        }
    }

    private void Trim(DateTimeOffset now)
    {
        while (_samples.Count > 0 && now - _samples.Peek() > Window)
        {
            _samples.Dequeue();
        }
    }

    private int RefreshCore(DateTimeOffset now)
    {
        Trim(now);
        CurrentCps = _samples.Count / 2;
        return CurrentCps;
    }
}

