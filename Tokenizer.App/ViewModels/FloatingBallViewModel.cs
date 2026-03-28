using Tokenizer.App.Services;
using Tokenizer.Core.Models;

namespace Tokenizer.App.ViewModels;

public sealed class FloatingBallViewModel(IAppDispatcher dispatcher) : ViewModelBase(dispatcher)
{
    private int _currentCps;
    private bool _isPaused;

    public int CurrentCps
    {
        get => _currentCps;
        private set => SetProperty(ref _currentCps, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set => SetProperty(ref _isPaused, value);
    }

    public string DisplayText => IsPaused ? "PAUSE" : $"{CurrentCps} c/s";

    public void ApplySnapshot(RealtimeStatsSnapshot snapshot)
    {
        IsPaused = snapshot.IsPaused;
        CurrentCps = snapshot.CurrentCps;
        OnPropertyChanged(nameof(DisplayText));
    }
}

