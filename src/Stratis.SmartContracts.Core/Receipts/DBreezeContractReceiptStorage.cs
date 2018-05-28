using DBreeze;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.SmartContracts.Core.Backend;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class DBreezeContractReceiptStorage : ISmartContractReceiptStorage
    {
        private const string TableName = "receipts";
        private readonly DBreezeEngine engine;

        public DBreezeContractReceiptStorage(DataFolder dataFolder)
        {
            this.engine = new DBreezeEngine(dataFolder.SmartContractStatePath + TableName);
        }

        /// <inheritdoc />
        public void SaveReceipt(uint256 txHash, ulong blockHeight, ISmartContractExecutionResult executionResult, uint160 contractAddress)
        {
            var receipt = new SmartContractReceipt(txHash, blockHeight, executionResult, contractAddress);

            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                t.Insert<byte[], byte[]>(TableName, txHash.ToBytes(), receipt.ToBytes());
                t.Commit();
            }
        }


        /// <inheritdoc />
        public SmartContractReceipt GetReceipt(uint256 txHash)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                byte[] result = t.Select<byte[],byte[]>(TableName, txHash.ToBytes()).Value;

                if (result == null)
                    return null;

                return SmartContractReceipt.FromBytes(result);
            }
        }
    }
}
