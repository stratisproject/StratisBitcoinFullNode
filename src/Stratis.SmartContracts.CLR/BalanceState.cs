using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Provides a contract's balance during execution, accounting for transfers generated while executing
    /// the contract
    /// </summary>
    public class BalanceState
    {
        private readonly IBalanceRepository repository;
        private readonly IReadOnlyList<TransferInfo> internalTransfers;

        public BalanceState(IBalanceRepository repository, IReadOnlyList<TransferInfo> internalTransfers)
        {
            this.repository = repository;
            this.internalTransfers = internalTransfers;
        }

        public BalanceState(IBalanceRepository repository, IReadOnlyList<TransferInfo> internalTransfers,
            TransferInfo initialTransfer)
        {
            this.repository = repository;
            this.internalTransfers = internalTransfers;
            this.InitialTransfer = initialTransfer;
        }

        /// <summary>
        /// The initial value transfer to the contract's address.
        /// </summary>
        public TransferInfo InitialTransfer { get; private set; }

        /// <summary>
        /// Adds a single value transfer to an address, which will be used in all future accounting.
        /// Used when performing an external contract create/call to reflect the value sent with
        /// the contract invocation transaction.
        /// </summary>
        public void AddInitialTransfer(TransferInfo transferInfo)
        {
            this.InitialTransfer = transferInfo;
        }

        public ulong GetBalance(uint160 address)
        {
            return this.repository.GetCurrentBalance(address) 
                   + this.GetPendingBalance(address);
        }

        private ulong GetPendingBalance(uint160 address)
        {
            ulong ret = this.InitialTransfer != null && this.InitialTransfer.To == address ? this.InitialTransfer.Value : 0UL;

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