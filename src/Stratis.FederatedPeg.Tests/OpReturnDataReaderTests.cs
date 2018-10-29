using System.Text;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class OpReturnDataReaderTests
    {
        private ILoggerFactory loggerFactory;

        private ApexRegTest network;

        private OpReturnDataReader opReturnDataReader;

        private readonly Key key;

        private BitcoinSecret sourceChainSecret;
        private BitcoinSecret targetChainSecret;

        private readonly BitcoinPubKeyAddress receiverAddress;

        public OpReturnDataReaderTests()
        {
            loggerFactory = Substitute.For<ILoggerFactory>();
            network = new ApexRegTest();
            opReturnDataReader = new OpReturnDataReader(loggerFactory, network);

            key = new Key();
            sourceChainSecret = network.CreateBitcoinSecret(key);
            targetChainSecret = network.ToCounterChainNetwork().CreateBitcoinSecret(key);
            receiverAddress = sourceChainSecret.GetAddress();
        }

        [Fact]
        public void GetStringFromOpReturn_CanReadAddress()
        {

            var opReturnAddress = targetChainSecret.GetAddress();
            var opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());

            var transaction = buildOpReturnTransaction(receiverAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Address);
            opReturnString.Should().Be(opReturnAddress.ToString());
        }

        [Fact]
        public void GetStringFromOpReturn_Can_NOT_ReadAddress_FromOwnNetwork()
        {
            var opReturnAddress = sourceChainSecret.GetAddress();
            var opReturnBytes = Encoding.UTF8.GetBytes(opReturnAddress.ToString());

            var transaction = buildOpReturnTransaction(this.receiverAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Unknown);
            opReturnString.Should().BeNull();
        }

        private static Transaction buildOpReturnTransaction(BitcoinPubKeyAddress receiverAddress, byte[] opReturnBytes)
        {
            var transaction = new Transaction();
            transaction.AddOutput(new TxOut(Money.COIN, receiverAddress));
            transaction.AddOutput(new TxOut(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes))));
            return transaction;
        }

        [Fact]
        public void GetStringFromOpReturn_CanReadTransactionHash()
        {
            var opReturnTransactionHash = buildTransaction(this.receiverAddress).GetHash();
            var opReturnBytes = opReturnTransactionHash.ToBytes();

            var transaction = buildOpReturnTransaction(this.receiverAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Hash);
            var expectedString = new uint256(opReturnBytes).ToString();
            opReturnString.Should().Be(expectedString);
        }

        [Fact]
        public void GetStringFromOpReturn_Can_NOT_Read_Transaction_with_two_OpReturns()
        {
            var opReturnAddress1 = sourceChainSecret.GetAddress();
            var opReturnBytes1 = Encoding.UTF8.GetBytes(opReturnAddress1.ToString());

            var transaction = buildOpReturnTransaction(this.receiverAddress, opReturnBytes1);

            var opReturnAddress2 = this.sourceChainSecret.GetAddress();
            var opReturnBytes2 = Encoding.UTF8.GetBytes(opReturnAddress2.ToString());
            transaction.AddOutput(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnBytes2)));

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Unknown);
            opReturnString.Should().BeNull();
        }

        [Fact]
        public void GetStringFromOpReturn_Can_NOT_ReadRandomStrings()
        {
            var opReturnBytes = Encoding.UTF8.GetBytes("neither hash, nor address");

            var transaction = buildOpReturnTransaction(this.receiverAddress, opReturnBytes);

            var opReturnString = this.opReturnDataReader.GetStringFromOpReturn(transaction, out OpReturnDataType opReturnDataType);

            opReturnDataType.Should().Be(OpReturnDataType.Unknown);
            opReturnString.Should().BeNull();
        }

        private static Transaction buildTransaction(BitcoinPubKeyAddress receiverAddress)
        {
            var transaction = new Transaction();
            transaction.AddOutput(new TxOut(Money.COIN, receiverAddress));
            return transaction;
        }
    }
}
