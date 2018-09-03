namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public interface IContractPrimitiveSerializer : ISerializer
    {
        byte[] Serialize(object obj); 
        T Deserialize<T>(byte[] stream);
    }
}
