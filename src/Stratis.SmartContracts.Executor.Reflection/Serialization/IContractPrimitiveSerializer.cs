using System;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public interface IContractPrimitiveSerializer : ISerializer
    {
        /// <summary>
        /// Serialize an 'unknown' type to bytes. 
        /// </summary>
        byte[] Serialize(object obj); 

        /// <summary>
        /// Serialize bytes to any primitive type.
        /// </summary>
        T Deserialize<T>(byte[] stream);
    }
}
