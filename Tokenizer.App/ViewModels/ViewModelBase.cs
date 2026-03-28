using CommunityToolkit.Mvvm.ComponentModel;
using Tokenizer.App.Services;

namespace Tokenizer.App.ViewModels;

public abstract class ViewModelBase(IAppDispatcher dispatcher) : ObservableObject
{
    protected IAppDispatcher Dispatcher { get; } = dispatcher;
}

