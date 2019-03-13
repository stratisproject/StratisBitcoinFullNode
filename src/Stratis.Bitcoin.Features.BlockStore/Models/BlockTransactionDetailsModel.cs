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

        public BlockTransactionDetailsModel(Block block, Network network, ChainBase chain) : base(block, chain)
        {
            this.Transactions = new TransactionVerboseModel[block.Transactions.Count];
            for (int i = 0; i < block.Transactions.Count; i++)
            {
                this.Transactions[i] = new TransactionVerboseModel(block.Transactions[i], network);
            }
        }

        public BlockTransactionDetailsModel()
        {
        }
    }
}
