using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Defines a data persistence strategy for a byte[] key value pair belonging to an address.
    /// Uses a GasMeter to perform accounting
    /// </summary>
    public class MeteredPersistenceStrategy : IPersistenceStrategy
    {
        private readonly IContractState stateDb;
        private readonly IGasMeter gasMeter;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;

        public MeteredPersistenceStrategy(IContractState stateDb, IGasMeter gasMeter, IKeyEncodingStrategy keyEncodingStrategy)
        {
            Guard.NotNull(stateDb, nameof(stateDb));
            Guard.NotNull(gasMeter, nameof(gasMeter));
            Guard.NotNull(gasMeter, nameof(keyEncodingStrategy));

            this.stateDb = stateDb;
            this.gasMeter = gasMeter;
            this.keyEncodingStrategy = keyEncodingStrategy;
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            byte[] encodedKey = this.keyEncodingStrategy.GetBytes(key);
            return this.stateDb.GetStorageValue(address, encodedKey);
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value)
        {
            byte[] encodedKey = this.keyEncodingStrategy.GetBytes(key);
            Gas operationCost = GasPriceList.StorageOperationCost(
                encodedKey,
                value);

            this.gasMeter.Spend(operationCost);
            this.stateDb.SetStorageValue(address, encodedKey, value);
        }
    }
}