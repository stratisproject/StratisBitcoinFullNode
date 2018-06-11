using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NBitcoin;
using Nethereum.RLP;
using Newtonsoft.Json;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Executor.Reflection.Serialization
{
    /// <summary>
    /// The end goal for this class is to take in any object and serialize it to bytes, or vice versa.
    /// Will likely need to be highly complex in the future but right now we just fall back to JSON worst case.
    /// This idea may be ridiculous so we can always have custom methods that have to be called on PersistentState in the future.
    /// </summary>
    public class PersistentStateSerializer
    {
        public static JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public byte[] Serialize(object o, Network network)
        {
            if (o is byte[])
                return (byte[])o;

            if (o is byte)
                return new byte[] { (byte)o };

            if (o is char)
                return new byte[] { Convert.ToByte(((char)o)) };

            if (o is Address)
                return ((Address)o).ToUint160(network).ToBytes();

            if (o is bool)
                return (BitConverter.GetBytes((bool)o));

            if (o is int)
                return BitConverter.GetBytes((int)o);

            if (o is long)
                return BitConverter.GetBytes((long)o);

            if (o is uint)
                return BitConverter.GetBytes((uint)o);

            if (o is ulong)
                return BitConverter.GetBytes((ulong)o);

            if (o is sbyte)
            {
                var bytes = BitConverter.GetBytes((sbyte) o);
                return BitConverter.GetBytes((sbyte)o);
            }

            if (o is string)
                return Encoding.UTF8.GetBytes((string)o);
            
            // This is obviously nasty, but our goal is to add custom data type support first and optimize later
            if (o.GetType().IsValueType)
                return SerializeType(o, network);
                //return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(o, Formatting.None, JsonSerializerSettings));

            throw new Exception(string.Format("{0} is not supported.", o.GetType().Name));
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
                return (object)stream;

            if (type == typeof(byte))
                return (object)stream[0];

            if (type == typeof(char))
                return (object)Convert.ToChar(stream[0]);

            if (type == typeof(Address))
                return (object)new uint160(stream).ToAddress(network);

            if (type == typeof(bool))
                return (object)(Convert.ToBoolean(stream[0]));

            if (type == typeof(int))
                return (object)(BitConverter.ToInt32(stream, 0));

            if (type == typeof(long))
                return (object)(BitConverter.ToInt64(stream, 0));

            if (type == typeof(sbyte))
                return (object)(byte[]) stream;

            if (type == typeof(string))
                return (object)(Encoding.UTF8.GetString(stream));

            if (type == typeof(uint))
                return (object)(BitConverter.ToUInt32(stream, 0));

            if (type == typeof(ulong))
                return (object)(BitConverter.ToUInt64(stream, 0));

            if (type.IsValueType)
                return DeserializeType(type, stream, network);
                //return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(stream), JsonSerializerSettings);

            throw new Exception(string.Format("{0} is not supported.", type.Name));
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
