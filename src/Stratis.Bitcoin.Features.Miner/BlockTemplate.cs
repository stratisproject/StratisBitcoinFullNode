using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Miner
{
    public sealed class BlockTemplate
    {
        public Block Block;

        public List<Money> VTxFees;

        public List<long> TxSigOpsCost;

        public string CoinbaseCommitment;

        public Money TotalFee;

        public BlockTemplate(Network network)
        {
            this.Block = network.Consensus.ConsensusFactory.CreateBlock();
            this.VTxFees = new List<Money>();
            this.TxSigOpsCost = new List<long>();
        }
    }
}