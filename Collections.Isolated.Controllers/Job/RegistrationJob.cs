using Collections.Isolated.Application.Commands.Logs;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Controllers.EventHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Collections.Isolated.Controllers.Job;

internal sealed class RegistrationJob(IServiceProvider provider) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = provider.CreateScope();

        var logClient = scope.ServiceProvider.GetRequiredService<ILogClient>();

        var consumer = scope.ServiceProvider.GetRequiredService<IConsumer>();

        var sendLog = new SendLog(logClient);

        NodeEventHandler handler = new(sendLog);

        handler.Register(consumer);
    }
}