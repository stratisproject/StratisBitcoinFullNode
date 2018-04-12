using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Core.State
{
    public class InternalTransferList
    {
        private readonly List<TransferInfo> pendingTransfers;

        public InternalTransferList()
        {
            this.pendingTransfers = new List<TransferInfo>();
        }

        public IList<TransferInfo> Transfers => this.pendingTransfers;

        public void Add(TransferInfo transfer)
        {
            this.pendingTransfers.Add(transfer);
        }

        public ulong GetPendingBalance(uint160 address)
        {
            ulong ret = 0;

            foreach (TransferInfo transfer in this.pendingTransfers.Where(x => x.To == address))
            {
                ret += transfer.Value;
            }

            foreach (TransferInfo transfer in this.pendingTransfers.Where(x => x.From == address))
            {
                ret -= transfer.Value;
            }

            return ret;
        }
    }
}