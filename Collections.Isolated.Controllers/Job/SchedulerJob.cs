using Collections.Isolated.Abstractions;
using Collections.Isolated.Domain.Consensus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Collections.Isolated.Controllers.Job;

internal sealed class SchedulerJob(IServiceProvider serviceProvider) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = serviceProvider.CreateScope();

        var nodeDictionaryContext = scope.ServiceProvider.GetRequiredService<IDictionaryContext<Node>>();



        nodeDictionaryContext.StateIntent();

        nodeDictionaryContext.TryGetAsync()

        handler.Register(consumer);
    }
}