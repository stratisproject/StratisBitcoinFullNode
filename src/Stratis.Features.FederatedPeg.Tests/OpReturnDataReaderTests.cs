using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class OpReturnDataReaderTests
    {
        private readonly ILoggerFactory loggerFactory;

        private readonly Network network;

        private readonly OpReturnDataReader opReturnDataReader;

        private readonly AddressHelper addressHelper;

        private readonly TestTransactionBuilder transactionBuilder;

        public OpReturnDataReaderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.network = FederatedPegNetwork.NetworksSelector.Regtest();
            this.opReturnDataReader = new OpReturnDataReader(this.loggerFactory, this.network);

            this.transactionBuilder = new TestTransactionBuilder();
            this.addressHelper = new AddressHelper(this.network);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void TryGetTargetAddressFromOpReturn_CanReadAddress()
        {

            BitcoinPubKeyAddress opReturnAddress = this.addressHelper.GetNewTargetChainPubKeyAddress();
            byte[] opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());
            Transaction transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            this.opReturnDataReader.TryGetTargetAddress(transaction, out string addressFromOpReturn);

            addressFromOpReturn.Should().Be(opReturnAddress.ToString());
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void TryGetTargetAddressFromOpReturn_Can_NOT_ReadAddress_FromOwnNetwork()
        {
            BitcoinPubKeyAddress opReturnAddress = this.addressHelper.GetNewSourceChainPubKeyAddress();
            byte[] opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());

            Transaction transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            this.opReturnDataReader.TryGetTargetAddress(transaction, out string opReturnString);

            opReturnString.Should().BeNull();
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void TryGetTargetAddressFromOpReturn_Can_NOT_Read_Transaction_with_two_valid_OpReturns_addresses()
        {
            BitcoinPubKeyAddress opReturnAddress1 = this.addressHelper.GetNewTargetChainPubKeyAddress();
            byte[] opReturnBytes1 = Encoding.UTF8.GetBytes(opReturnAddress1.ToString());

            Transaction transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes1);

            BitcoinPubKeyAddress opReturnAddress2 = this.addressHelper.GetNewTargetChainPubKeyAddress();
            opReturnAddress1.ToString().Should().NotBe(
                opReturnAddress2.ToString(), "otherwise the transaction is not ambiguous");
            byte[] opReturnBytes2 = Encoding.UTF8.GetBytes(opReturnAddress2.ToString());
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes2)));

            this.opReturnDataReader.TryGetTargetAddress(transaction, out string addressFromOpReturn);
            addressFromOpReturn.Should().BeNull();
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void TryGetTargetAddressFromOpReturn_Can_Read_Transaction_with_many_OpReturns_but_only_a_valid_address_one()
        {
            BitcoinPubKeyAddress opReturnValidAddress = this.addressHelper.GetNewTargetChainPubKeyAddress();
            byte[] opReturnValidAddressBytes = Encoding.UTF8.GetBytes(opReturnValidAddress.ToString());

            Transaction transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnValidAddressBytes);

            //address 2 will be ignored as not valid for target chain
            byte[] opReturnInvalidAddressBytes = Encoding.UTF8.GetBytes(this.addressHelper.GetNewSourceChainPubKeyAddress().ToString());
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnInvalidAddressBytes)));

            //add another output with the same target address, this is not ambiguous
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnValidAddressBytes)));

            //add other random message
            byte[] randomMessageBytes = Encoding.UTF8.GetBytes("neither hash, nor address");
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(randomMessageBytes)));

            this.opReturnDataReader.TryGetTargetAddress(transaction, out string addressFromOpReturn);
            addressFromOpReturn.Should().Be(opReturnValidAddress.ToString());
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void TryGetTargetAddressFromOpReturn_Can_NOT_ReadRandomStrings()
        {
            byte[] opReturnBytes = Encoding.UTF8.GetBytes("neither hash, nor address");
            Transaction transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            this.opReturnDataReader.TryGetTargetAddress(transaction, out string opReturnString);

            opReturnString.Should().BeNull();
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void TryGetTransactionIdFromOpReturn_Can_NOT_Read_Random_Strings()
        {
            byte[] opReturnBytes = Encoding.UTF8.GetBytes("neither hash, nor address");
            Transaction transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            this.opReturnDataReader.TryGetTransactionId(transaction, out string opReturnString);

            opReturnString.Should().BeNull();
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void TryGetTransactionIdFromOpReturn_Can_NOT_Read_Two_Valid_uint256_OpReturns()
        {
            uint256 opReturnTransactionHash1 = this.transactionBuilder.BuildTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress()).GetHash();
            byte[] opReturnBytes1 = opReturnTransactionHash1.ToBytes();

            uint256 opReturnTransactionHash2 = this.transactionBuilder.BuildTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress()).GetHash();
            byte[] opReturnBytes2 = opReturnTransactionHash2.ToBytes();

            opReturnBytes2.Should().NotBeEquivalentTo(opReturnBytes1, "otherwise there is no ambiguity");

            Transaction transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes1);
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes2)));

            this.opReturnDataReader.TryGetTransactionId(transaction, out string opReturnString);

            opReturnString.Should().BeNull();
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void TryGetTransactionIdFromOpReturn_Can_Read_many_OpReturns_with_only_one_valid_uint256()
        {
            uint256 opReturnTransactionHash1 = this.transactionBuilder.BuildTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress()).GetHash();
            byte[] opReturnBytes1 = opReturnTransactionHash1.ToBytes();

            byte[] opReturnBytes2 = Encoding.UTF8.GetBytes("neither hash, nor address");

            Transaction transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes1);
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes2)));

            this.opReturnDataReader.TryGetTransactionId(transaction, out string opReturnString);

            opReturnString.Should().NotBeNull();
            opReturnString.Should().Be(new uint256(opReturnBytes1).ToString());
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void TryGetTransactionIdFromOpReturn_Can_Read_single_OpReturn_with_valid_uint256()
        {
            uint256 opReturnTransactionHash = this.transactionBuilder.BuildTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress()).GetHash();
            byte[] opReturnBytes = opReturnTransactionHash.ToBytes();

            Transaction transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            this.opReturnDataReader.TryGetTransactionId(transaction, out string opReturnString);

            opReturnString.Should().NotBeNull();
            opReturnString.Should().Be(new uint256(opReturnBytes).ToString());
        }
    }
}