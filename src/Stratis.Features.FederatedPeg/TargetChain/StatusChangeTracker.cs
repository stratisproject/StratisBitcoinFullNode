using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class StatusChangeTracker : Dictionary<ICrossChainTransfer, CrossChainTransferStatus?>
    {
        /// <summary>
        /// Records changes to transfers for the purpose of synchronizing the transient lookups after the DB commit.
        /// </summary>
        /// <param name="transfer">The cross-chain transfer to update.</param>
        /// <param name="status">The new status.</param>
        /// <param name="blockHash">The block hash of the partialTranction.</param>
        /// <param name="blockHeight">The block height of the partialTransaction.</param>
        /// <remarks>
        /// Within the store the earliest status is <see cref="CrossChainTransferStatus.Partial"/>. In this case <c>null</c>
        /// is used to flag a new transfer - a transfer with no earlier status. <c>null</c> is not written to the DB.
        /// </remarks>
        public void SetTransferStatus(ICrossChainTransfer transfer, CrossChainTransferStatus? status = null, uint256 blockHash = null, int blockHeight = 0)
        {
            if (status != null)
            {
                // If setting the status then record the previous status.
                this[transfer] = transfer.Status;
                transfer.SetStatus((CrossChainTransferStatus)status, blockHash, blockHeight);
            }
            else
            {
                // If not setting the status then assume there is no previous status.
                this[transfer] = null;
            }
        }

        /// <summary>
        /// Returns a list of unique block hashes for the transfers being tracked.
        /// </summary>
        /// <returns>A list of unique block hashes for the transfers being tracked.</returns>
        public uint256[] UniqueBlockHashes()
        {
            return this.Keys.Where(k => k.BlockHash != null).Select(k => k.BlockHash).Distinct().ToArray();
        }
    }
}
