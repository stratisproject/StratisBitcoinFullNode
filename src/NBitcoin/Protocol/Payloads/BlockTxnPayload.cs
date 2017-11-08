using System.Collections.Generic;

namespace NBitcoin.Protocol
{
    [Payload("blocktxn")]
    public class BlockTxnPayload : Payload
    {
        private uint256 blockId;
        public uint256 BlockId
        {
            get
            {
                return this.blockId;
            }
            set
            {
                this.blockId = value;
            }
        }

        private List<Transaction> transactions = new List<Transaction>();
        public List<Transaction> Transactions
        {
            get
            {
                return this.transactions;
            }
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.blockId);
            stream.ReadWrite(ref this.transactions);
        }
    }
}