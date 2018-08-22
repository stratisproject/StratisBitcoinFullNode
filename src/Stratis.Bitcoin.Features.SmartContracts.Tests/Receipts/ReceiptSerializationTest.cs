using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Receipts
{
    public class ReceiptSerializationTest
    {
        [Fact]
        public void Receipt_Serializes_And_Deserializes()
        {
            var noLogReceipt = new Receipt(new uint256(1234), 12345, new Log[] { });

            TestSerializeReceipt(noLogReceipt);
        }

        private void TestSerializeReceipt(Receipt receipt)
        {
            byte[] serialized = receipt.ToBytesRlp();
            Receipt deserialized = Receipt.FromBytesRlp(serialized);
            Assert.Equal(receipt.PostState, deserialized.PostState);
            Assert.Equal(receipt.GasUsed, deserialized.GasUsed);
            Assert.Equal(receipt.Bloom, deserialized.Bloom);
            // TODO: Logs
        }
    }
}
