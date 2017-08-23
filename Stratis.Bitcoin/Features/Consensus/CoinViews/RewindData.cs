using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    public class RewindData : IBitcoinSerializable
    {
        private uint256 previousBlockHash;
        public uint256 PreviousBlockHash
        {
            get
            {
                return this.previousBlockHash;
            }
            set
            {
                this.previousBlockHash = value;
            }
        }

        List<uint256> transactionsToRemove = new List<uint256>();
        public List<uint256> TransactionsToRemove
        {
            get
            {
                return this.transactionsToRemove;
            }
            set
            {
                this.transactionsToRemove = value;
            }
        }

        List<UnspentOutputs> outputsToRestore = new List<UnspentOutputs>();
        public List<UnspentOutputs> OutputsToRestore
        {
            get
            {
                return this.outputsToRestore;
            }
            set
            {
                this.outputsToRestore = value;
            }
        }

        public RewindData()
        {
        }

        public RewindData(uint256 previousBlockHash)
        {
            this.previousBlockHash = previousBlockHash;
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.previousBlockHash);
            stream.ReadWrite(ref this.transactionsToRemove);
            stream.ReadWrite(ref this.outputsToRestore);
        }
    }
}
