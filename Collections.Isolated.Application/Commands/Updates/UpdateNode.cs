using Collections.Isolated.Abstractions;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Consensus;

namespace Collections.Isolated.Application.Commands.Updates;

internal sealed class UpdateNode(IDictionaryContext<Node> dictionaryContext, IUnitOfWork<Node> unitOfWork)
{
    internal async Task Handle(string nodeId)
    {
        var node = await dictionaryContext.TryGetAsync(nodeId);

        if (node is null)
        {
            throw new InvalidOperationException($"Node with id {nodeId} does not exist");
        }

        node.UpdateTimeout();

        await dictionaryContext.AddOrUpdateAsync(node.Id, node);

        await unitOfWork.SaveChangesAsync();
    }
}