using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Provides a contract's balance during execution, accounting for transfers generated while executing
    /// the contract
    /// </summary>
    public class BalanceState
    {
        private readonly IBalanceRepository repository;
        private readonly IReadOnlyList<TransferInfo> internalTransfers;

        public BalanceState(IBalanceRepository repository, ulong txAmount, IReadOnlyList<TransferInfo> internalTransfers)
        {
            this.repository = repository;
            this.TxAmount = txAmount;
            this.internalTransfers = internalTransfers;
        }
        
        public ulong TxAmount { get; }

        public ulong GetBalance(uint160 address)
        {
            return this.repository.GetCurrentBalance(address) 
                   + this.TxAmount 
                   + this.GetPendingBalance(address);
        }

        private ulong GetPendingBalance(uint160 address)
        {
            ulong ret = 0;

            foreach (TransferInfo transfer in this.internalTransfers.Where(x => x.To == address))
            {
                ret += transfer.Value;
            }

            foreach (TransferInfo transfer in this.internalTransfers.Where(x => x.From == address))
            {
                ret -= transfer.Value;
            }

            return ret;
        }
    }
}