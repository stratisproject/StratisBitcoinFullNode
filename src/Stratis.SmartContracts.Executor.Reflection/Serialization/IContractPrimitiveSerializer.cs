﻿using System;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public interface IContractPrimitiveSerializer : ISerializer
    {
        byte[] Serialize(object obj); 
        T Deserialize<T>(byte[] stream);
        object Deserialize(Type type, byte[] stream);
    }
}
