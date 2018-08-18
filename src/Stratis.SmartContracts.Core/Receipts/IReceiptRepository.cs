using NBitcoin;

namespace Stratis.SmartContracts.Core.Receipts
{
    public interface IReceiptRepository
    {
        /*
         *Validating a block:

          For each transaction:
            - Generate a receipt
            - Store receipt in memory

          Hash all receipts, and generate merkle root.
          Ensure that merkle root matches block receipt root.
          Store receipt in DBreeze.

          TODO: Worry about pruning old data in case of chain reorg. 
          TODO: Worry about indexing. 
         * 
         */

        Receipt GetReceipt(uint256 txHash);

        void SaveReceipt(Receipt receipt);
    }
}
