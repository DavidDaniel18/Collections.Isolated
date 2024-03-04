namespace Collections.Isolated.Domain.Common.Seedwork.Interfaces;

public interface IDeepCloneable<T> where T : class
{
    T DeepClone();
}