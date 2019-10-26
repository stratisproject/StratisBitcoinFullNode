using System.Collections.Generic;
using System.IO;
using DBreeze;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class PersistentReceiptRepository : IReceiptRepository
    {
        private const string TableName = "receipts";
        private readonly DBreezeEngine engine;

        public PersistentReceiptRepository(DataFolder dataFolder)
        {
            string folder = dataFolder.SmartContractStatePath + TableName;
            Directory.CreateDirectory(folder);
            this.engine = new DBreezeEngine(folder);
        }

        // TODO: Handle pruning old data in case of reorg.

        /// <inheritdoc />
        public void Store(IEnumerable<Receipt> receipts)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                foreach(Receipt receipt in receipts)
                {
                    t.Insert<byte[], byte[]>(TableName, receipt.TransactionHash.ToBytes(), receipt.ToStorageBytesRlp());
                }
                t.Commit();
            }
        }

        /// <inheritdoc />
        public Receipt Retrieve(uint256 hash)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                return this.GetReceipt(t, hash);
            }
        }

        /// <inheritdoc />
        public IList<Receipt> RetrieveMany(IList<uint256> hashes)
        {
            List<Receipt> ret = new List<Receipt>();
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                foreach(uint256 hash in hashes)
                {
                    ret.Add(this.GetReceipt(t, hash));
                }

                return ret;
            }
        }

        private Receipt GetReceipt(DBreeze.Transactions.Transaction t, uint256 hash)
        {
            byte[] result = t.Select<byte[], byte[]>(TableName, hash.ToBytes()).Value;

            if (result == null)
                return null;

            return Receipt.FromStorageBytesRlp(result);
        }
    }
}
