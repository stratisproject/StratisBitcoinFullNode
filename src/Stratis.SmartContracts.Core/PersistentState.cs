using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.Serialization;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core
{
    public class PersistentState : IPersistentState
    {
        public IContractStateRepository StateDb { get; private set; }

        public uint160 ContractAddress { get; }
        private static readonly PersistentStateSerializer serializer = new PersistentStateSerializer();
        private readonly IPersistenceStrategy persistenceStrategy;
        private readonly Network network;

        /// <summary>
        /// Instantiate a new PersistentState instance. Each PersistentState object represents
        /// a slice of state for a particular contract address.
        /// </summary>
        /// <param name="stateDb"></param>
        /// <param name="persistenceStrategy"></param>
        /// <param name="contractAddress"></param>
        public PersistentState(IContractStateRepository stateDb, IPersistenceStrategy persistenceStrategy, uint160 contractAddress, Network network)
        {
            this.StateDb = stateDb;
            this.persistenceStrategy = persistenceStrategy;
            this.ContractAddress = contractAddress;
            this.network = network;
        }

        public T GetObject<T>(string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] bytes = this.persistenceStrategy.FetchBytes(this.ContractAddress, keyBytes);

            if (bytes == null)
                return default(T);

            return serializer.Deserialize<T>(bytes);
        }

        public void SetObject<T>(string key, T obj)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            this.persistenceStrategy.StoreBytes(this.ContractAddress, keyBytes, serializer.Serialize(obj, this.network));
        }

        public ISmartContractMapping<V> GetMapping<V>(string name)
        {
            return new SmartContractMapping<V>(this, name);
        }

        public ISmartContractList<T> GetList<T>(string name)
        {
            return new SmartContractList<T>(this, name);
        }
    }
}