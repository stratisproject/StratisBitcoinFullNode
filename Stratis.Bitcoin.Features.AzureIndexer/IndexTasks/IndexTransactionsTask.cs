using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NBitcoin.Indexer.IndexTasks
{
    public class IndexTransactionsTask : IndexTableEntitiesTaskBase<TransactionEntry.Entity>
    {
        public IndexTransactionsTask(IndexerConfiguration configuration)
            : base(configuration)
        {
        }


        protected override void ProcessBlock(BlockInfo block, BulkImport<TransactionEntry.Entity> bulk)
        {
            foreach (var transaction in block.Block.Transactions)
            {
                var indexed = new TransactionEntry.Entity(null, transaction, block.BlockId);
                bulk.Add(indexed.PartitionKey, indexed);
            }
        }

        protected override CloudTable GetCloudTable()
        {
            return Configuration.GetTransactionTable();
        }

        protected override ITableEntity ToTableEntity(TransactionEntry.Entity indexed)
        {
            return indexed.CreateTableEntity();
        }
    }
}
