using System.Collections.Generic;
using DBreeze;
using Stratis.Bitcoin.Configuration;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class ReceiptRepository : IReceiptRepository
    {
        private const string TableName = "receipts";
        private readonly DBreezeEngine engine;

        public ReceiptRepository(DataFolder dataFolder)
        {
            this.engine = new DBreezeEngine(dataFolder.SmartContractStatePath + TableName);
        }

        // TODO: Handle pruning old data in case of reorg.

        /// <inheritdocs />
        public void Store(IEnumerable<Receipt> receipts)
        {
            // TODO: Optionally store receipts.
        }
    }
}
