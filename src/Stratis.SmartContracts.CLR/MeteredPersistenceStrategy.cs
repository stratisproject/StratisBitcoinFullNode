using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Defines a data persistence strategy for a byte[] key value pair belonging to an address.
    /// Uses a GasMeter to perform accounting
    /// </summary>
    public class MeteredPersistenceStrategy : IPersistenceStrategy
    {
        private readonly IStateRepository stateDb;
        private readonly RuntimeObserver.IGasMeter gasMeter;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;

        public MeteredPersistenceStrategy(IStateRepository stateDb, RuntimeObserver.IGasMeter gasMeter, IKeyEncodingStrategy keyEncodingStrategy)
        {
            Guard.NotNull(stateDb, nameof(stateDb));
            Guard.NotNull(gasMeter, nameof(gasMeter));
            Guard.NotNull(gasMeter, nameof(keyEncodingStrategy));

            this.stateDb = stateDb;
            this.gasMeter = gasMeter;
            this.keyEncodingStrategy = keyEncodingStrategy;
        }

        public bool ContractExists(uint160 address)
        {
            this.gasMeter.Spend((RuntimeObserver.Gas)GasPriceList.StorageCheckContractExistsCost);

            return this.stateDb.IsExist(address);
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            byte[] encodedKey = this.keyEncodingStrategy.GetBytes(key);
            byte[] value = this.stateDb.GetStorageValue(address, encodedKey);

            RuntimeObserver.Gas operationCost = GasPriceList.StorageRetrieveOperationCost(encodedKey, value);
            this.gasMeter.Spend(operationCost);

            return value;
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value)
        {
            byte[] encodedKey = this.keyEncodingStrategy.GetBytes(key);
            RuntimeObserver.Gas operationCost = GasPriceList.StorageSaveOperationCost(
                encodedKey,
                value);

            this.gasMeter.Spend(operationCost);
            this.stateDb.SetStorageValue(address, encodedKey, value);
        }
    }
}