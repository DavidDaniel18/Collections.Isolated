namespace Collections.Isolated.Application.Converters.Interfaces;

public interface IMappingTo<in TDto, out TAggregate> where TDto : class where TAggregate : class
{
    TAggregate MapFrom(TDto dto);
}