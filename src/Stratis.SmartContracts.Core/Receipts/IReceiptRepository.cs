using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.Core.Receipts
{
    public interface IReceiptRepository
    {
        /// <summary>
        /// Permanently store several receipts.
        /// </summary>
        void Store(IEnumerable<Receipt> receipts);

        /// <summary>
        /// Retrieve a receipt by transaction hash.
        /// </summary>
        Receipt Retrieve(uint256 txHash);
    }
}
