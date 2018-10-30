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

        private ApexRegTest network;

        private OpReturnDataReader opReturnDataReader;

        private AddressHelper addressHelper;

        private TestTransactionBuilder transactionBuilder;

        public OpReturnDataReaderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.network = new ApexRegTest();
            this.opReturnDataReader = new OpReturnDataReader(this.loggerFactory, this.network);

            this.transactionBuilder = new TestTransactionBuilder();
            this.addressHelper = new AddressHelper(this.network);
        }

        [Fact]
        public void GetStringFromOpReturn_CanReadAddress()
        {

            var opReturnAddress = this.addressHelper.TargetChainAddress;
            var opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Address);
            opReturnString.Should().Be(opReturnAddress.ToString());
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_CanReadAddress()
        {

            var opReturnAddress = this.addressHelper.TargetChainAddress;
            var opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());
            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnBytes);

            var addressFromOpReturn = this.opReturnDataReader.TryGetTargetAddressFromOpReturn(transaction);

            addressFromOpReturn.Should().Be(opReturnAddress.ToString());
        }

        [Fact]
        public void GetStringFromOpReturn_Can_NOT_ReadAddress_FromOwnNetwork()
        {
            var opReturnAddress = this.addressHelper.GetNewSourceChainAddress();
            var opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Unknown);
            opReturnString.Should().BeNull();
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_Can_NOT_ReadAddress_FromOwnNetwork()
        {
            var opReturnAddress = this.addressHelper.GetNewSourceChainAddress();
            var opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.TryGetTargetAddressFromOpReturn(transaction);

            opReturnString.Should().BeNull();
        }

        [Fact]
        public void GetStringFromOpReturn_CanReadTransactionHash()
        {
            var opReturnTransactionHash = this.transactionBuilder.BuildTransaction(this.addressHelper.SourceChainAddress).GetHash();
            var opReturnBytes = opReturnTransactionHash.ToBytes();

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Hash);
            var expectedString = new uint256(opReturnBytes).ToString();
            opReturnString.Should().Be(expectedString);
        }

        [Fact]
        public void GetStringFromOpReturn_Can_NOT_Read_Transaction_with_two_OpReturns()
        {
            var opReturnAddress1 = this.addressHelper.TargetChainAddress;
            var opReturnBytes1 = Encoding.UTF8.GetBytes(opReturnAddress1.ToString());

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnBytes1);

            var opReturnAddress2 = this.addressHelper.GetNewTargetChainAddress();
            var opReturnBytes2 = Encoding.UTF8.GetBytes(opReturnAddress2.ToString());
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes2)));

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Unknown);
            opReturnString.Should().BeNull();
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_Can_NOT_Read_Transaction_with_two_valid_OpReturns_addresses()
        {
            var opReturnAddress1 = this.addressHelper.TargetChainAddress;
            var opReturnBytes1 = Encoding.UTF8.GetBytes(opReturnAddress1.ToString());

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnBytes1);

            var opReturnAddress2 = this.addressHelper.GetNewTargetChainAddress();
            opReturnAddress1.ToString().Should().NotBe(
                opReturnAddress2.ToString(), "otherwise the transaction is not ambiguous");
            var opReturnBytes2 = Encoding.UTF8.GetBytes(opReturnAddress2.ToString());
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes2)));

            var addressFromOpReturn = this.opReturnDataReader.TryGetTargetAddressFromOpReturn(transaction);
            addressFromOpReturn.Should().BeNull();
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_Can_Read_Transaction_with_many_OpReturns_but_only_a_valid_address_one()
        {
            var opReturnValidAddress = this.addressHelper.TargetChainAddress;
            var opReturnValidAddressBytes = Encoding.UTF8.GetBytes(opReturnValidAddress.ToString());

            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnValidAddressBytes);

            //address 2 will be ignored as not valid for target chain
            var opReturnInvalidAddressBytes = Encoding.UTF8.GetBytes(this.addressHelper.GetNewSourceChainAddress().ToString());
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnInvalidAddressBytes)));

            //add another output with the same target address, this is not ambiguous
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnValidAddressBytes)));

            //add other random message
            var randomMessageBytes = Encoding.UTF8.GetBytes("neither hash, nor address");
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(randomMessageBytes)));

            var addressFromOpReturn = this.opReturnDataReader.TryGetTargetAddressFromOpReturn(transaction);
            addressFromOpReturn.Should().Be(this.addressHelper.TargetChainAddress.ToString());
        }

        [Fact]
        public void GetStringFromOpReturn_Can_NOT_ReadRandomStrings()
        {
            var opReturnBytes = Encoding.UTF8.GetBytes("neither hash, nor address");
            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Unknown);
            opReturnString.Should().BeNull();
        }

        [Fact]
        public void TryGetTargetAddressFromOpReturn_Can_NOT_ReadRandomStrings()
        {
            var opReturnBytes = Encoding.UTF8.GetBytes("neither hash, nor address");
            var transaction = this.transactionBuilder.BuildOpReturnTransaction(this.addressHelper.SourceChainAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.TryGetTargetAddressFromOpReturn(transaction);

            opReturnString.Should().BeNull();
        }
    }
}