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

        /// <summary>
        /// Retrieves the receipt for each of the given IDs. It will put null in an index
        /// if that hash is not found in the database.
        /// </summary>
        IList<Receipt> RetrieveMany(IList<uint256> hashes);
    }
}
