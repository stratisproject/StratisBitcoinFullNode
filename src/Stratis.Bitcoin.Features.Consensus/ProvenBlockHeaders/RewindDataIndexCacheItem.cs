using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class RewindDataIndexItem
    {
        public uint256 TransactionId { get; set; }
        public uint TransactionOutputIndex { get; set; }

        public RewindDataIndexItem(uint256 transactionId, uint transactionOutputIndex)
        {
            this.TransactionId = transactionId;
            this.TransactionOutputIndex = transactionOutputIndex;
        }

        public override int GetHashCode()
        {
            return this.TransactionId.GetHashCode() + (int)this.TransactionOutputIndex;
        }

        public override bool Equals(object obj)
        {
            var item = obj as RewindDataIndexItem;
            if (item == null)
                return false;

            return this.TransactionOutputIndex == item.TransactionOutputIndex &&
                   this.TransactionId.Equals(item.TransactionId);
        }
    }
}
