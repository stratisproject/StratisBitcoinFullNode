using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    /// <summary>
    /// This class serializes and deserializes specific data types
    /// when persisting items inside a smart contract.
    /// </summary>
    public class PersistentStateSerializer
    {
        public byte[] Serialize(object o, Network network)
        {
            if (o is null)
                return new byte[0];

            if (o is byte[] bytes)
                return bytes;

            if (o is byte b1)
                return new byte[] { b1 };

            if (o is char c)
                return new byte[] { Convert.ToByte(c) };

            if (o is Address address)
                return address.ToUint160(network).ToBytes();

            if (o is bool b)
                return (BitConverter.GetBytes(b));

            if (o is int i)
                return BitConverter.GetBytes(i);

            if (o is long l)
                return BitConverter.GetBytes(l);

            if (o is uint u)
                return BitConverter.GetBytes(u);

            if (o is ulong ul)
                return BitConverter.GetBytes(ul);

            if (o is string s)
                return Encoding.UTF8.GetBytes(s);
            
            if (o.GetType().IsValueType)
                return SerializeType(o, network);
                
            throw new PersistentStateSerializationException(string.Format("{0} is not supported.", o.GetType().Name));
        }

        private byte[] SerializeType(object o, Network network)
        {
            List<byte[]> toEncode = new List<byte[]>(); 

            foreach (FieldInfo field in o.GetType().GetFields())
            {
                object value = field.GetValue(o);
                byte[] serialized = Serialize(value, network);
                toEncode.Add(RLP.EncodeElement(serialized));
            }

            return RLP.EncodeList(toEncode.ToArray());
        }

        public T Deserialize<T>(byte[] stream, Network network)
        {
            object deserialized = Deserialize(typeof(T), stream, network);
            if (deserialized == null)
                return default(T);

            return (T) deserialized;
        }

        private object Deserialize(Type type, byte[] stream, Network network)
        {
            if (stream == null || stream.Length == 0)
                return null;

            if (type == typeof(byte[]))
                return stream;

            if (type == typeof(byte))
                return stream[0];

            if (type == typeof(char))
                return Convert.ToChar(stream[0]);

            if (type == typeof(Address))
                return new uint160(stream).ToAddress(network);

            if (type == typeof(bool))
                return Convert.ToBoolean(stream[0]);

            if (type == typeof(int))
                return BitConverter.ToInt32(stream, 0);

            if (type == typeof(long))
                return BitConverter.ToInt64(stream, 0);

            if (type == typeof(string))
                return Encoding.UTF8.GetString(stream);

            if (type == typeof(uint))
                return BitConverter.ToUInt32(stream, 0);

            if (type == typeof(ulong))
                return BitConverter.ToUInt64(stream, 0);

            if (type.IsValueType)
                return DeserializeType(type, stream, network);
                
            throw new PersistentStateSerializationException(string.Format("{0} is not supported.", type.Name));
        }

        private object DeserializeType(Type type, byte[] bytes, Network network)
        {
            RLPCollection collection = (RLPCollection) RLP.Decode(bytes)[0];

            var ret = Activator.CreateInstance(type);

            FieldInfo[] fields = type.GetFields();

            for (int i = 0; i < fields.Length; i++)
            {
                byte[] fieldBytes = collection[i].RLPData;
                fields[i].SetValue(ret, Deserialize(fields[i].FieldType, fieldBytes, network));
            }

            return ret;
        }
    }
}
