using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.RLP;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    public class PrimitiveArraySerializer
    {
        private readonly IPrimitiveSerializer primitiveSerializer;

        public PrimitiveArraySerializer(IPrimitiveSerializer primitiveSerializer)
        {
            this.primitiveSerializer = primitiveSerializer;
        }

        public byte[] Serialize(Array array)
        {
            // Edge case, serializing nonsensical
            if (array is byte[] a)
                return a;

            List<byte[]> toEncode = new List<byte[]>();

            for (int i = 0; i < array.Length; i++)
            {
                object value = array.GetValue(i);
                byte[] serialized = null; // Serialize(value);
                toEncode.Add(RLP.EncodeElement(serialized));
            }

            return RLP.EncodeList(toEncode.ToArray());
        }

        public T[] ToArray<T>(byte[] val)
        {
            throw new NotImplementedException();
        }
    }
}
