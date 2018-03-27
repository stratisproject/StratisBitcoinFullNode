using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Defines a data persistence strategy for a byte[] key value pair belonging to an address.
    /// Uses a GasMeter to perform accounting
    /// </summary>
    public class MeteredPersistenceStrategy : IPersistenceStrategy
    {
        private readonly IContractStateRepository stateDb;
        private readonly IGasMeter gasMeter;

        public MeteredPersistenceStrategy(IContractStateRepository stateDb, IGasMeter gasMeter)
        {
            Guard.NotNull(stateDb, nameof(stateDb));
            Guard.NotNull(gasMeter, nameof(gasMeter));

            this.stateDb = stateDb;
            this.gasMeter = gasMeter;
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            //byte[] hashedKey = HashHelper.Keccak256(key);
            return this.stateDb.GetStorageValue(address, key);
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value)
        {
            //byte[] hashedKey = HashHelper.Keccak256(key);
            Gas operationCost = GasPriceList.StorageOperationCost(
                key,
                value);

            this.gasMeter.Spend(operationCost);
            this.stateDb.SetStorageValue(address, key, value);
        }
    }
}