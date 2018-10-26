using System;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public interface IContractPrimitiveSerializer
    {
        byte[] Serialize(object obj); 
        T Deserialize<T>(byte[] stream);
        object Deserialize(Type type, byte[] stream);
        Address ToAddress(string address);
    }
}
