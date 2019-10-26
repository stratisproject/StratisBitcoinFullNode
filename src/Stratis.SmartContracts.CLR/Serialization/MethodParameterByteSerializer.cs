using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.RLP;

namespace Stratis.SmartContracts.CLR.Serialization
{
    /// <summary>
    /// Serializes method parameters using RLP-encoded byte arrays.
    /// </summary>
    public class MethodParameterByteSerializer : IMethodParameterSerializer
    {
        private readonly IContractPrimitiveSerializer primitiveSerializer;

        public MethodParameterByteSerializer(IContractPrimitiveSerializer primitiveSerializer)
        {
            this.primitiveSerializer = primitiveSerializer;
        }

        public byte[] Serialize(object[] methodParameters)
        {
            if (methodParameters == null)
                throw new ArgumentNullException(nameof(methodParameters));

            var result = new List<byte[]>();

            foreach (object param in methodParameters)
            {
                byte[] encoded = this.Encode(param);

                result.Add(encoded);
            }

            return RLP.EncodeList(result.Select(RLP.EncodeElement).ToArray());
        }

        public object[] Deserialize(byte[] bytes)
        {
            RLPCollection list = RLP.Decode(bytes);

            RLPCollection innerList = (RLPCollection)list[0];

            IList<byte[]> encodedParamBytes = innerList.Select(x => x.RLPData).ToList();

            var results = new List<object>();

            foreach (byte[] encodedParam in encodedParamBytes)
            {
                object result = this.Decode(encodedParam);

                results.Add(result);
            }
            
            return results.ToArray();
        }

        private byte[] Encode(object o)
        {
            Prefix prefix = Prefix.ForObject(o);

            byte[] serializedBytes = this.primitiveSerializer.Serialize(o);

            var result = new byte[prefix.Length + serializedBytes.Length];

            prefix.CopyTo(result);

            serializedBytes.CopyTo(result, prefix.Length);

            return result;
        }

        private object Decode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (bytes.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(bytes));

            var prefix = new Prefix(bytes[0]);

            byte[] paramBytes = bytes.Skip(prefix.Length).ToArray();

            return this.primitiveSerializer.Deserialize(prefix.Type, paramBytes);
        }
    }
}