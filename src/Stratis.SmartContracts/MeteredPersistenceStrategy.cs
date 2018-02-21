using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.State;
using NBitcoin;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Defines a data persistence strategy for a byte[] key value pair belonging to an address.
    /// Uses a GasMeter to perform accounting
    /// </summary>
    public class MeteredPersistenceStrategy : IPersistenceStrategy
    {
        private readonly IContractStateRepository stateDb;
        private readonly GasMeter gasMeter;

        public MeteredPersistenceStrategy(IContractStateRepository stateDb, GasMeter gasMeter)
        {
            this.stateDb = stateDb;
            this.gasMeter = gasMeter;
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            return this.stateDb.GetStorageValue(address, key); ;
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value)
        {
            Gas operationCost = GasPriceList.StorageOperationCost(
                key,
                value);

            this.gasMeter.Spend(operationCost);

            this.stateDb.SetStorageValue(address, key, value);
        }
    }
}