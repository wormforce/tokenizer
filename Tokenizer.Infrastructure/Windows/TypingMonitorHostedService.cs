using Microsoft.Extensions.Hosting;

namespace Tokenizer.Infrastructure.Windows;

public sealed class TypingMonitorHostedService(TypingMonitorService service) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => service.InitializeAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await service.FlushAsync(cancellationToken);
        await service.DisposeAsync();
    }
}

