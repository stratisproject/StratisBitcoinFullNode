using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class DepositExtractorTests
    {
        private IFederationGatewaySettings settings;

        private IOpReturnDataReader opReturnDataReader;

        private ILoggerFactory loggerFactory;

        private readonly IFullNode fullNode;

        private DepositExtractor depositExtractor;

        private Network network;

        private MultisigAddressHelper addressHelper;

        private TestTransactionBuilder transactionBuilder;

        private readonly ConcurrentChain chain;

        public DepositExtractorTests()
        {
            this.network = FederatedPegNetwork.NetworksSelector.Regtest();

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.settings = Substitute.For<IFederationGatewaySettings>();
            this.opReturnDataReader = Substitute.For<IOpReturnDataReader>();
            this.fullNode = Substitute.For<IFullNode>();
            this.fullNode.NodeService<ConcurrentChain>().Returns(this.chain);

            this.addressHelper = new MultisigAddressHelper(this.network);

            this.settings.MultiSigRedeemScript.Returns(this.addressHelper.SourceChainMultisigAddress.ScriptPubKey);
            this.opReturnDataReader.TryGetTargetAddress(null).ReturnsForAnyArgs((string)null);

            this.transactionBuilder = new TestTransactionBuilder();

            this.depositExtractor = new DepositExtractor(
                this.loggerFactory, 
                this.settings, 
                this.opReturnDataReader,
                this.fullNode);
        }

        [Fact]
        public void ExtractDepositsFromBlock_Should_Only_Find_Deposits_To_Multisig()
        {
            var block = this.network.Consensus.ConsensusFactory.CreateBlock();

            var targetAddress = this.addressHelper.GetNewTargetChainPubKeyAddress();
            var opReturnBytes = Encoding.UTF8.GetBytes(targetAddress.ToString());
            long depositAmount = Money.COIN * 3;
            var depositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, opReturnBytes, depositAmount);
            block.AddTransaction(depositTransaction);

            this.opReturnDataReader.TryGetTargetAddress(depositTransaction)
                .Returns(targetAddress.ToString());

            var nonDepositTransactionToMultisig = this.transactionBuilder.BuildTransaction(
                this.addressHelper.SourceChainMultisigAddress);
            block.AddTransaction(nonDepositTransactionToMultisig);

            var otherAddress = this.addressHelper.GetNewSourceChainPubKeyAddress();
            otherAddress.ToString().Should().NotBe(this.addressHelper.SourceChainMultisigAddress.ToString(),
                "otherwise the next deposit should actually be extracted");
            var depositTransactionToOtherAddress =
                this.transactionBuilder.BuildOpReturnTransaction(otherAddress, opReturnBytes);
            block.AddTransaction(depositTransactionToOtherAddress);

            var nonDepositTransactionToOtherAddress = this.transactionBuilder.BuildTransaction(
                otherAddress);
            block.AddTransaction(nonDepositTransactionToOtherAddress);

            int blockHeight = 230;
            var extractedDeposits = this.depositExtractor.ExtractDepositsFromBlock(block, blockHeight);

            extractedDeposits.Count.Should().Be(1);
            var extractedTransaction = extractedDeposits[0];

            extractedTransaction.Amount.Satoshi.Should().Be(depositAmount);
            extractedTransaction.Id.Should().Be(depositTransaction.GetHash());
            extractedTransaction.TargetAddress.Should().Be(targetAddress.ToString());
            extractedTransaction.BlockNumber.Should().Be(blockHeight);
            extractedTransaction.BlockHash.Should().Be(block.GetHash());
        }

        [Fact]
        public void ExtractDepositsFromBlock_Should_Create_One_Deposit_Per_Transaction_To_Multisig()
        {
            var block = this.network.Consensus.ConsensusFactory.CreateBlock();

            var targetAddress = this.addressHelper.GetNewTargetChainPubKeyAddress();
            var opReturnBytes = Encoding.UTF8.GetBytes(targetAddress.ToString());
            long depositAmount = Money.COIN * 3;
            var depositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, opReturnBytes, depositAmount);
            block.AddTransaction(depositTransaction);

            this.opReturnDataReader.TryGetTargetAddress(depositTransaction)
                .Returns(targetAddress.ToString());

            //add another deposit to the same address
            long secondDepositAmount = Money.COIN * 2;
            var secondDepositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, opReturnBytes, secondDepositAmount);
            block.AddTransaction(secondDepositTransaction);
            this.opReturnDataReader.TryGetTargetAddress(secondDepositTransaction)
                .Returns(targetAddress.ToString());

            //add another deposit to a different address
            var newTargetAddress = this.addressHelper.GetNewTargetChainPubKeyAddress().ToString();
            var newOpReturnBytes = Encoding.UTF8.GetBytes(newTargetAddress);
            long thirdDepositAmount = Money.COIN * 34;
            var thirdDepositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, newOpReturnBytes, thirdDepositAmount);
            block.AddTransaction(thirdDepositTransaction);

            this.opReturnDataReader.TryGetTargetAddress(thirdDepositTransaction)
                .Returns(newTargetAddress);

            int blockHeight = 12345;
            var extractedDeposits = this.depositExtractor.ExtractDepositsFromBlock(block, blockHeight);

            extractedDeposits.Count.Should().Be(3);
            extractedDeposits.Select(d => d.BlockNumber).Should().AllBeEquivalentTo(blockHeight);
            extractedDeposits.Select(d => d.BlockHash).Should().AllBeEquivalentTo(block.GetHash());

            var extractedTransaction = extractedDeposits[0];
            extractedTransaction.Amount.Satoshi.Should().Be(depositAmount);
            extractedTransaction.Id.Should().Be(depositTransaction.GetHash());
            extractedTransaction.TargetAddress.Should()
                .Be(targetAddress.ToString());

            extractedTransaction = extractedDeposits[1];
            extractedTransaction.Amount.Satoshi.Should().Be(secondDepositAmount);
            extractedTransaction.Id.Should().Be(secondDepositTransaction.GetHash());
            extractedTransaction.TargetAddress.Should()
                .Be(targetAddress.ToString());

            extractedTransaction = extractedDeposits[2];
            extractedTransaction.Amount.Satoshi.Should().Be(thirdDepositAmount);
            extractedTransaction.Id.Should().Be(thirdDepositTransaction.GetHash());
            extractedTransaction.TargetAddress.Should().Be(newTargetAddress);
        }
    }
}
