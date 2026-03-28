namespace Tokenizer.App.Services;

public interface IAppDispatcher
{
    Task EnqueueAsync(Action action);

    Task<T> EnqueueAsync<T>(Func<T> action);
}

