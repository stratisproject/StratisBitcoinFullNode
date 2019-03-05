using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Creates a new <see cref="State"/> object.
    /// </summary>
    public class StateFactory : IStateFactory
    {
        private readonly IInternalExecutorFactory internalTransactionExecutorFactory;
        private readonly ISmartContractStateFactory smartContractStateFactory;

        public StateFactory(ISmartContractStateFactory smartContractStateFactory)
        {
            this.smartContractStateFactory = smartContractStateFactory;
        }

        public IState Create(IStateRepository stateRoot, IBlock block, ulong txOutValue, uint256 transactionHash)
        {
            var logHolder = new ContractLogHolder();
            var internalTransfers = new List<TransferInfo>();
            return new State(this.smartContractStateFactory, stateRoot, logHolder, internalTransfers, block, transactionHash);
        }
    }
}