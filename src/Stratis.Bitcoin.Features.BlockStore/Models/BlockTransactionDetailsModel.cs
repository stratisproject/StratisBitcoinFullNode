using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Controllers.Models;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public class BlockTransactionDetailsModel : BlockModel
    {
        /// <summary>
        /// Hides the existing Transactions property of type <see cref="string[]"/> and replaces with the <see cref="TransactionVerboseModel[]"/>.
        /// </summary>
        public new TransactionVerboseModel[] Transactions { get; set; }

        public BlockTransactionDetailsModel(Block block, ChainedHeader chainedHeader, ChainedHeader tip, Network network) : base(block, chainedHeader, tip, network)
        {
            this.Transactions = block.Transactions.Select(trx => new TransactionVerboseModel(trx, network)).ToArray();
        }

        public BlockTransactionDetailsModel()
        {
        }
    }
}
