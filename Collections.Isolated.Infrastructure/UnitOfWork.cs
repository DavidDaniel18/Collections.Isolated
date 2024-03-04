using Collections.Isolated.Abstractions;
using Collections.Isolated.Application.Interfaces;
using Collections.Isolated.Domain.Common.Seedwork.Abstract;

namespace Collections.Isolated.Infrastructure;

internal sealed class UnitOfWork<TAggregate>(IPublisher publisher, IDictionaryContext<TAggregate> dictionaryContext) : IUnitOfWork<TAggregate> 
    where TAggregate : Aggregate<TAggregate>
{
    public async Task SaveChangesAsync()
    {
        try
        {
            var entities = dictionaryContext.GetTrackedEntities();

            foreach (var entity in entities)
            {
                foreach (var @event in entity.DomainEvents)
                {
                    await publisher.Publish(@event).ConfigureAwait(false);
                }
            }

            await dictionaryContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            dictionaryContext.RollBack();

            throw new InvalidOperationException("Error saving changes in Unit of Work", e);
        }
    }
}