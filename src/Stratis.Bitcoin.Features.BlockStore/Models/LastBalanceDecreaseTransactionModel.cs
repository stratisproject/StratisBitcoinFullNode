using Stratis.Bitcoin.Controllers.Models;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public class LastBalanceDecreaseTransactionModel
    {
        public TransactionVerboseModel Transaction { get; set; }

        public int BlockHeight { get; set; }
    }
}
