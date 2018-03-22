using System;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts.Core.Serialization
{
    /// <summary>
    /// The end goal for this class is to take in any object and serialize it to bytes, or vice versa.
    /// Will likely need to be highly complex in the future but right now we just fall back to JSON worst case.
    /// This idea may be ridiculous so we can always have custom methods that have to be called on PersistentState in the future.
    /// </summary>
    public class PersistentStateSerializer
    {
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
                return BitConverter.GetBytes((sbyte)o);

            if (o is string)
                return Encoding.UTF8.GetBytes((string)o);

            throw new Exception(string.Format("{0} is not supported.", o.GetType().Name));
        }

        public T Deserialize<T>(byte[] stream, Network network)
        {
            if (stream == null || stream.Length == 0)
                return default(T);

            if (typeof(T) == typeof(byte[]))
                return (T)(object)stream;

            if (typeof(T) == typeof(byte))
                return (T)(object)stream[0];

            if (typeof(T) == typeof(char))
                return (T)(object)Convert.ToChar(stream[0]);

            if (typeof(T) == typeof(Address))
                return (T)(object)new Address(new BitcoinPubKeyAddress(new KeyId(stream),network).ToString());

            if (typeof(T) == typeof(bool))
                return (T)(object)(Convert.ToBoolean(stream[0]));

            if (typeof(T) == typeof(int))
                return (T)(object)(BitConverter.ToInt32(stream, 0));

            if (typeof(T) == typeof(long))
                return (T)(object)(BitConverter.ToInt64(stream, 0));

            if (typeof(T) == typeof(sbyte))
                return (T)(object)(Convert.ToSByte(stream[0]));

            if (typeof(T) == typeof(string))
                return (T)(object)(Encoding.UTF8.GetString(stream));

            if (typeof(T) == typeof(uint))
                return (T)(object)(BitConverter.ToUInt32(stream, 0));

            if (typeof(T) == typeof(ulong))
                return (T)(object)(BitConverter.ToUInt64(stream, 0));

            throw new Exception(string.Format("{0} is not supported.", typeof(T).Name));
        }
    }
}
