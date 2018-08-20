using System.Collections.Generic;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class ReceiptRepository : IReceiptRepository
    {
        // If this gets implemented:
        // TODO: Handle pruning old data in case of reorg.
        // TODO: Indexing to improve retrieval speed (for Web3 mainly).

        public void Store(IEnumerable<Receipt> receipts)
        {
            // TODO: Optionally store receipts.
        }
    }
}
