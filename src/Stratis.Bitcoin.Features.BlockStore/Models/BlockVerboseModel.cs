using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Controllers.Models;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public class BlockVerboseModel : BlockModel
    {
        public BlockVerboseModel(Block block, Network network) : base(block)
        {
            this.Transactions = block.Transactions.Select(trx => new TransactionVerboseModel(trx, network)).ToArray();
        }

        /// <summary>
        /// Hides the existing Transactions property of type string[] and replaces with the TransactionVerboseModel[].
        /// </summary>
        public new TransactionVerboseModel[] Transactions { get; set; }
    }
}
