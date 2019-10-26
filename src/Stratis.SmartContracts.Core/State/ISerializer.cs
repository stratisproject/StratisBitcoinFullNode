namespace Stratis.SmartContracts.Core.State
{
    public interface ISerializer<T, S>
    {
        S Serialize(T obj);
        T Deserialize(S stream);
    }
}
