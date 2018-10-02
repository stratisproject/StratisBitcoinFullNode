using System;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    /// <summary>
    /// Serializes method parameters in a byte-encoded format using an <see cref="IContractPrimitiveSerializer"/>.
    /// </summary>
    public sealed class MethodParameterByteSerializer : IMethodParameterSerializer
    {
        private readonly IContractPrimitiveSerializer primitiveSerializer;

        public MethodParameterByteSerializer(IContractPrimitiveSerializer primitiveSerializer)
        {
            this.primitiveSerializer = primitiveSerializer;
        }

        public byte[] Serialize(object[] methodParameters)
        {
            return new byte[] { };
        }

        public object[] Deserialize(string[] parameters)
        {
            // Don't bother implementing this as it will be removed soon.
            throw new NotImplementedException();
        }

        public object[] Deserialize(byte[] methodParameters)
        {
            return new object[] { };
        }
    }
}