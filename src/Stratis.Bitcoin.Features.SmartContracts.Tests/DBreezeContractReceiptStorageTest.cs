using System;
using System.IO;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class DBreezeContractReceiptStorageTest
    {
        private readonly DBreezeContractReceiptStorage receiptStorage;

        public DBreezeContractReceiptStorageTest()
        {
            var folder = TestBase.AssureEmptyDir(Path.Combine(AppContext.BaseDirectory, "TestData"));
            this.receiptStorage = new DBreezeContractReceiptStorage(new DataFolder(folder));
        }

        [Fact]
        public void ReceiptStorage_General_Use()
        {
            // Test that we can save and retrieve a receipt, even if everything but the transaction hash is null. 
            var txContextMock = new Mock<ISmartContractTransactionContext>();
            txContextMock.Setup(x=>x.TransactionHash).Returns(() => new uint256(0));
            ISmartContractTransactionContext txContext = txContextMock.Object;
            ISmartContractExecutionResult result = new Mock<ISmartContractExecutionResult>().Object;
            this.receiptStorage.SaveReceipt(txContext, result);
            SmartContractReceipt receipt = this.receiptStorage.GetReceipt(txContext.TransactionHash);
            Assert.NotNull(receipt);
        }
    }
}
