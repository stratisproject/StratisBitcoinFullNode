using System;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public interface IContractPrimitiveSerializer : ISerializer
    {
        byte[] Serialize<T>(T obj);

        byte[] Serialize(Type type, object o);

        T Deserialize<T>(byte[] stream);
    }
}
