using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer.IndexTasks
{
    public class IndexBalanceTask : IndexTableEntitiesTaskBase<OrderedBalanceChange>
    {
        WalletRuleEntryCollection _WalletRules;
        public IndexBalanceTask(IndexerConfiguration conf, WalletRuleEntryCollection walletRules)
            : base(conf)
        {
            _WalletRules = walletRules;
        }
        protected override Microsoft.WindowsAzure.Storage.Table.CloudTable GetCloudTable()
        {
            return Configuration.GetBalanceTable();
        }

        protected override Microsoft.WindowsAzure.Storage.Table.ITableEntity ToTableEntity(OrderedBalanceChange item)
        {
            return item.ToEntity();
        }

        protected override bool SkipToEnd
        {
            get
            {
                return _WalletRules != null && _WalletRules.Count == 0;
            }
        }

        protected override void ProcessBlock(BlockInfo block, BulkImport<OrderedBalanceChange> bulk)
        {
            foreach (var tx in block.Block.Transactions)
            {
                var txId = tx.GetHash();

                var entries = extract(txId, tx, block.BlockId, block.Block.Header, block.Height);
                foreach (var entry in entries)
                {
                    bulk.Add(entry.PartitionKey, entry);
                }
            }
        }

        private IEnumerable<OrderedBalanceChange> extract(uint256 txId, Transaction tx, uint256 blockId, BlockHeader blockHeader, int height)
        {
            if (_WalletRules != null)
                return OrderedBalanceChange.ExtractWalletBalances(txId, tx, blockId, blockHeader, height, _WalletRules);
            else
                return OrderedBalanceChange.ExtractScriptBalances(txId, tx, blockId, blockHeader, height);
        }
    }
}

