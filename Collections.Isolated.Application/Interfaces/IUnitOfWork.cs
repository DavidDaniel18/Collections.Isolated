using Collections.Isolated.Domain.Common.Seedwork.Abstract;

namespace Collections.Isolated.Application.Interfaces;

internal interface IUnitOfWork<TAggregate> where TAggregate : Aggregate<TAggregate>
{
    Task SaveChangesAsync();
}