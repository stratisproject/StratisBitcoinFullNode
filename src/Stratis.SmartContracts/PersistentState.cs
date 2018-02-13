using Stratis.SmartContracts.State;
using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using DBreeze;

namespace Stratis.SmartContracts
{
    public class PersistentState
    {
        internal IContractStateRepository StateDb { get; private set; }

        private uint _counter;
        private static readonly PersistentStateSerializer serializer = new PersistentStateSerializer();

        /// <summary>
        /// Instantiate a new PersistentState instance. Each PersistentState object represents
        /// a slice of state for a particular contract address.
        /// </summary>
        /// <param name="stateDb"></param>
        /// <param name="contractAddress"></param>
        public PersistentState(IContractStateRepository stateDb, uint160 contractAddress)
        {
            StateDb = stateDb;
            ContractAddress = contractAddress;
            this._counter = 0;
        }

        public uint160 ContractAddress { get; }

        public T GetObject<T>(object key)
        {
            byte[] keyBytes = serializer.Serialize(key);
            byte[] bytes = StateDb.GetStorageValue(this.ContractAddress, keyBytes);

            if (bytes == null)
                return default(T);

            return serializer.Deserialize<T>(bytes);
        }

        public void SetObject<T>(object key, T obj)
        {
            byte[] keyBytes = serializer.Serialize(key);
            StateDb.SetStorageValue(this.ContractAddress, keyBytes, serializer.Serialize(obj));
        }

        public SmartContractMapping<K,V> GetMapping<K, V>()
        {
            return new SmartContractMapping<K, V>(this, this._counter++);
        }

        public SmartContractList<T> GetList<T>()
        {
            return new SmartContractList<T>(this, this._counter++);
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

            if (o is string)
                return Encoding.UTF8.GetBytes((string) o);

            return Encoding.UTF8.GetBytes(NetJSON.NetJSON.Serialize(o));
        }

        public T Deserialize<T>(byte[] stream)
        {
            if (stream == null || stream.Length == 0)
                return default(T);

            if (typeof(T) == typeof(byte[]))
                return (T) (object) stream;

            if (typeof(T) == typeof(byte))
                return (T)(object)stream[0];

            if (typeof(T) == typeof(char))
                return (T)(object)Convert.ToChar(stream[0]);

            if (typeof(T) == typeof(Address))
                return (T) (object) new Address(new uint160(stream));

            if (typeof(T) == typeof(bool))
                return (T) (object) (Convert.ToBoolean(stream[0]));

            if (typeof(T) == typeof(string))
                return (T)(object)(Encoding.UTF8.GetString(stream));

            return NetJSON.NetJSON.Deserialize<T>(Encoding.UTF8.GetString(stream));
        }
    }
}
