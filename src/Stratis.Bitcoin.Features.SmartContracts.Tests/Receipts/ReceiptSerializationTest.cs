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
            // create - to is null.
            var createReceipt = new Receipt(
                new uint256(123),
                new uint256(1234),
                new uint160(12345),
                null,
                new uint160(12347),
                10_000,
                true,
                null);

            TestSerializeReceipt(createReceipt);

            // call - newcontract is null.
            var callReceipt = new Receipt(
                new uint256(123),
                new uint256(1234),
                new uint160(12345),
                new uint160(12347),
                null,
                10_000,
                true,
                null);

            TestSerializeReceipt(callReceipt);
        }

        private void TestSerializeReceipt(Receipt receipt)
        {
            byte[] serialized = receipt.ToBytesRlp();
            Receipt deserialized = Receipt.FromBytesRlp(serialized);
            Assert.Equal(receipt.BlockHash, deserialized.BlockHash);
            Assert.Equal(receipt.GasUsed, deserialized.GasUsed);
            Assert.Equal(receipt.NewContractAddress, deserialized.NewContractAddress);
            Assert.Equal(receipt.ReturnValue, deserialized.ReturnValue);
            Assert.Equal(receipt.Sender, deserialized.Sender);
            Assert.Equal(receipt.To, deserialized.To);
            Assert.Equal(receipt.TransactionId, deserialized.TransactionId);
        }
    }
}
