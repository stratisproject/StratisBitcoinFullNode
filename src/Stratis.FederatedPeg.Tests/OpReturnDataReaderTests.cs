using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class OpReturnDataReaderTests
    {
        private ILoggerFactory loggerFactory;

        private Network network;

        private OpReturnDataReader opReturnDataReader;

        private AddressHelper addressHelper;

        private TestTransactionBuilder transactionBuilder;

        public OpReturnDataReaderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.network = FederatedPegNetwork.NetworksSelector.Regtest();
            this.opReturnDataReader = new OpReturnDataReader(this.loggerFactory, this.network);

            this.transactionBuilder = new TestTransactionBuilder();
            this.addressHelper = new AddressHelper(this.network);
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_CanReadAddress()
        {

            var opReturnAddress = this.addressHelper.GetNewTargetChainPubKeyAddress();
            var opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());
            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            var addressFromOpReturn = this.opReturnDataReader.TryGetTargetAddress(transaction);

            addressFromOpReturn.Should().Be(opReturnAddress.ToString());
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_Can_NOT_ReadAddress_FromOwnNetwork()
        {
            var opReturnAddress = this.addressHelper.GetNewSourceChainPubKeyAddress();
            var opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            var opReturnString = this.opReturnDataReader.TryGetTargetAddress(transaction);

            opReturnString.Should().BeNull();
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_Can_NOT_Read_Transaction_with_two_valid_OpReturns_addresses()
        {
            var opReturnAddress1 = this.addressHelper.GetNewTargetChainPubKeyAddress();
            var opReturnBytes1 = Encoding.UTF8.GetBytes(opReturnAddress1.ToString());

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes1);

            var opReturnAddress2 = this.addressHelper.GetNewTargetChainPubKeyAddress();
            opReturnAddress1.ToString().Should().NotBe(
                opReturnAddress2.ToString(), "otherwise the transaction is not ambiguous");
            var opReturnBytes2 = Encoding.UTF8.GetBytes(opReturnAddress2.ToString());
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes2)));

            var addressFromOpReturn = this.opReturnDataReader.TryGetTargetAddress(transaction);
            addressFromOpReturn.Should().BeNull();
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_Can_Read_Transaction_with_many_OpReturns_but_only_a_valid_address_one()
        {
            var opReturnValidAddress = this.addressHelper.GetNewTargetChainPubKeyAddress();
            var opReturnValidAddressBytes = Encoding.UTF8.GetBytes(opReturnValidAddress.ToString());

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnValidAddressBytes);

            //address 2 will be ignored as not valid for target chain
            var opReturnInvalidAddressBytes = Encoding.UTF8.GetBytes(this.addressHelper.GetNewSourceChainPubKeyAddress().ToString());
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnInvalidAddressBytes)));

            //add another output with the same target address, this is not ambiguous
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnValidAddressBytes)));

            //add other random message
            var randomMessageBytes = Encoding.UTF8.GetBytes("neither hash, nor address");
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(randomMessageBytes)));

            var addressFromOpReturn = this.opReturnDataReader.TryGetTargetAddress(transaction);
            addressFromOpReturn.Should().Be(opReturnValidAddress.ToString());
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_Can_NOT_ReadRandomStrings()
        {
            var opReturnBytes = Encoding.UTF8.GetBytes("neither hash, nor address");
            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            var opReturnString = this.opReturnDataReader.TryGetTargetAddress(transaction);

            opReturnString.Should().BeNull();
        }

        [Fact]
        public void TryGetTransactionIdFromOpReturn_Can_NOT_Read_Random_Strings()
        {
            var opReturnBytes = Encoding.UTF8.GetBytes("neither hash, nor address");
            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            var opReturnString = this.opReturnDataReader.TryGetTransactionId(transaction);

            opReturnString.Should().BeNull();
        }

        [Fact]
        public void TryGetTransactionIdFromOpReturn_Can_NOT_Read_Two_Valid_uint256_OpReturns()
        {
            var opReturnTransactionHash1 = this.transactionBuilder.BuildTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress()).GetHash();
            var opReturnBytes1 = opReturnTransactionHash1.ToBytes();

            var opReturnTransactionHash2 = this.transactionBuilder.BuildTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress()).GetHash();
            var opReturnBytes2 = opReturnTransactionHash2.ToBytes();

            opReturnBytes2.Should().NotBeEquivalentTo(opReturnBytes1, "otherwise there is no ambiguity");

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes1);
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes2)));

            var opReturnString = this.opReturnDataReader.TryGetTransactionId(transaction);

            opReturnString.Should().BeNull();
        }

        [Fact]
        public void TryGetTransactionIdFromOpReturn_Can_Read_many_OpReturns_with_only_one_valid_uint256()
        {
            var opReturnTransactionHash1 = this.transactionBuilder.BuildTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress()).GetHash();
            var opReturnBytes1 = opReturnTransactionHash1.ToBytes();

            var opReturnBytes2 = Encoding.UTF8.GetBytes("neither hash, nor address");

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes1);
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes2)));

            var opReturnString = this.opReturnDataReader.TryGetTransactionId(transaction);

            opReturnString.Should().NotBeNull();
            opReturnString.Should().Be(new uint256(opReturnBytes1).ToString());
        }

        [Fact]
        public void TryGetTransactionIdFromOpReturn_Can_Read_single_OpReturn_with_valid_uint256()
        {
            var opReturnTransactionHash = this.transactionBuilder.BuildTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress()).GetHash();
            var opReturnBytes = opReturnTransactionHash.ToBytes();

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.GetNewSourceChainPubKeyAddress(), opReturnBytes);

            var opReturnString = this.opReturnDataReader.TryGetTransactionId(transaction);

            opReturnString.Should().NotBeNull();
            opReturnString.Should().Be(new uint256(opReturnBytes).ToString());
        }
    }
}