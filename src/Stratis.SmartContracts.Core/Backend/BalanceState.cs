using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Provides a contract's balance during execution, accounting for transfers generated while executing
    /// the contract
    /// </summary>
    public class BalanceState
    {
        private readonly IBalanceRepository repository;
        private readonly InternalTransferList internalTransfers;
        private readonly ulong txAmount;

        public BalanceState(IBalanceRepository repository, ulong txAmount, InternalTransferList internalTransfers)
        {
            this.repository = repository;
            this.txAmount = txAmount;
            this.internalTransfers = internalTransfers;
        }

        public ulong GetBalance(uint160 address)
        {
            return this.repository.GetCurrentBalance(address) 
                   + this.txAmount 
                   + this.GetPendingBalance(address);
        }

        private ulong GetPendingBalance(uint160 address)
        {
            ulong ret = 0;

            foreach (TransferInfo transfer in this.internalTransfers.Transfers.Where(x => x.To == address))
            {
                ret += transfer.Value;
            }

            foreach (TransferInfo transfer in this.internalTransfers.Transfers.Where(x => x.From == address))
            {
                ret -= transfer.Value;
            }

            return ret;
        }
    }
}