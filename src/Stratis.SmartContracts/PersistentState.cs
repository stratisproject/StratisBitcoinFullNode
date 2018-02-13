using System;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.State;

namespace Stratis.SmartContracts
{
    public static class PersistentState
    {
        internal static IContractStateRepository StateDb { get; private set; }

        private static uint160 contractAddress;
        private static uint counter;
        private static PersistentStateSerializer serializer = new PersistentStateSerializer();

        internal static void SetDbAndAddress(IContractStateRepository stateDb, uint160 contractAddress)
        {
            StateDb = stateDb;
            PersistentState.contractAddress = contractAddress;
        }

        internal static void SetAddress(uint160 contractAddress)
        {
            PersistentState.contractAddress = contractAddress;
        }

        public static T GetObject<T>(object key)
        {
            byte[] keyBytes = serializer.Serialize(key);
            byte[] bytes = StateDb.GetStorageValue(contractAddress, keyBytes);

            if (bytes == null)
                return default(T);

            return serializer.Deserialize<T>(bytes);
        }

        public static void SetObject<T>(object key, T obj)
        {
            byte[] keyBytes = serializer.Serialize(key);
            StateDb.SetStorageValue(contractAddress, keyBytes, serializer.Serialize(obj));
        }

        public static SmartContractMapping<K, V> GetMapping<K, V>()
        {
            return new SmartContractMapping<K, V>(PersistentState.counter++);
        }

        public static SmartContractList<T> GetList<T>()
        {
            return new SmartContractList<T>(PersistentState.counter++);
        }

        internal static void ResetCounter()
        {
            PersistentState.counter = 0;
        }
    }

    /// <summary>
    /// The end goal for this class is to take in any object and serialize it to bytes, or vice versa.
    /// Will likely need to be highly complex in the future but right now we just fall back to JSON worst case.
    /// This idea may be ridiculous so we can always have custom methods that have to be called on PersistentState in the future.
    /// </summary>
    public class PersistentStateSerializer
    {
        // TODO: Fill in all so that JSON isn't put in
        public byte[] Serialize(object o)
        {
            if (o is byte[])
                return (byte[])o;

            if (o is byte)
                return new byte[] { (byte)o };

            if (o is char)
                return new byte[] { Convert.ToByte(((char)o)) };

            if (o is Address)
                return ((Address)o).ToUint160().ToBytes();

            if (o is bool)
                return (BitConverter.GetBytes((bool)o));

            if (o is int)
                return BitConverter.GetBytes((int)o);

            if (o is string)
                return Encoding.UTF8.GetBytes((string)o);

            return Encoding.UTF8.GetBytes(NetJSON.NetJSON.Serialize(o));
        }

        public T Deserialize<T>(byte[] stream)
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
                return (T)(object)new Address(new uint160(stream));

            if (typeof(T) == typeof(bool))
                return (T)(object)(Convert.ToBoolean(stream[0]));

            if (typeof(T) == typeof(string))
                return (T)(object)(Encoding.UTF8.GetString(stream));

            return NetJSON.NetJSON.Deserialize<T>(Encoding.UTF8.GetString(stream));
        }
    }
}
