using Collections.Isolated.Abstractions;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Common.Interfaces;
using Collections.Isolated.Domain.Consensus;

namespace Collections.Isolated.Application.Commands.Registration;

internal sealed class RegisterNode(
    IHostInfo hostInfo, 
    IDatetimeProvider datetimeProvider, 
    ILogging logging, 
    IDictionaryContext<Node> dictionaryContext,
    IUnitOfWork<Node> unitOfWork)
{
    internal async Task Handle()
    {
        var node = new Node(
            Guid.NewGuid().ToString(),
            hostInfo.GetClusterSize(),
            datetimeProvider,
            logging);

        dictionaryContext.StateIntent([node.Id], false);

        if (await dictionaryContext.TryGetAsync(node.Id) is not null)
        {
            throw new InvalidOperationException($"Node with id {node.Id} already exists");
        }

        await dictionaryContext.AddOrUpdateAsync(node.Id, node);

        node.UpdateTimeout();

        await unitOfWork.SaveChangesAsync();
    }
}