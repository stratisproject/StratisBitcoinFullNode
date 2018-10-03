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

        public BalanceState(IBalanceRepository repository, IReadOnlyList<TransferInfo> internalTransfers)
        {
            this.repository = repository;
            this.internalTransfers = internalTransfers;
        }

        public BalanceState(IBalanceRepository repository, IReadOnlyList<TransferInfo> internalTransfers,
            (ulong, uint160) initialBalance)
        {
            this.repository = repository;
            this.internalTransfers = internalTransfers;
            this.InitialBalance = initialBalance;
        }

        public (ulong, uint160) InitialBalance { get; private set; }

        public void AddInitialBalance(ulong messageAmount, uint160 address)
        {
            this.InitialBalance = (messageAmount, address);
        }

        public ulong GetBalance(uint160 address)
        {
            return this.repository.GetCurrentBalance(address) 
                   + this.GetPendingBalance(address);
        }

        private ulong GetPendingBalance(uint160 address)
        {
            (var balance, var contractAddress) = this.InitialBalance;

            ulong ret = address == contractAddress ? balance : 0UL;

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