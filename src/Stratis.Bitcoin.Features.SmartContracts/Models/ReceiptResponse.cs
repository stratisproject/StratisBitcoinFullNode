using Stratis.SmartContracts.Core.Receipts;

namespace Stratis.Bitcoin.Features.SmartContracts.Models
{
    public class ReceiptResponse
    {
        public string TransactionHash { get; }
        public string BlockHash { get; }
        public string PostState { get; }
        public ulong GasUsed { get; }
        public string Bloom { get; }

        public ReceiptResponse(Receipt receipt)
        {
            //this.TransactionHash = receipt.TransactionHash.ToString();
            //this.BlockHash = receipt.BlockHash.ToString();
            //this.PostState = receipt.PostState.ToString();
            //this.GasUsed = receipt.GasUsed;
            //this.Bloom = receipt.Bloom.ToString();
            //receipt.


        }
    }
}
