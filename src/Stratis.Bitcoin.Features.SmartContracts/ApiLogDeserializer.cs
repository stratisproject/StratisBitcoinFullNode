using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class ApiLogDeserializer
    {
        private readonly IContractPrimitiveSerializer primitiveSerializer;
        private readonly Network network;

        public ApiLogDeserializer(IContractPrimitiveSerializer primitiveSerializer, Network network)
        {
            this.primitiveSerializer = primitiveSerializer;
            this.network = network;
        }

        public dynamic DeserializeLogData(byte[] bytes, Type type)
        {
            RLPCollection collection = (RLPCollection)RLP.Decode(bytes)[0];

            var instance = new ExpandoObject() as IDictionary<string, object>;

            FieldInfo[] fields = type.GetFields();

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                byte[] fieldBytes = collection[i].RLPData;
                Type fieldType = field.FieldType;

                if (fieldType == typeof(Address))
                {
                    string base58Address = new uint160(fieldBytes).ToBase58Address(this.network);

                    instance[field.Name] = base58Address;
                }
                else
                {
                    object fieldValue = this.primitiveSerializer.Deserialize(fieldType, fieldBytes);

                    instance[field.Name] = fieldValue;
                }
            }

            return instance;
        }
    }
}