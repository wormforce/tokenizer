using Tokenizer.App.Services;

namespace Tokenizer.App.ViewModels;

public sealed class ShellViewModel(IAppDispatcher dispatcher) : ViewModelBase(dispatcher)
{
    public string AppTitle => "Tokenizer";

    public string Subtitle => "Windows typing analytics";
}

