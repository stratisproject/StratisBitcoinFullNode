using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    using System.Collections.Generic;

    public class WithdrawalExtractorTests
    {
        private IFederationGatewaySettings settings;

        private IOpReturnDataReader opReturnDataReader;

        private ILoggerFactory loggerFactory;

        private IWithdrawalReceiver withdrawalReceiver;

        private WithdrawalExtractor withdrawalExtractor;

        private Network network;

        private MultisigAddressHelper addressHelper;

        private TestMultisigTransactionBuilder transactionBuilder;

        public WithdrawalExtractorTests()
        {
            this.network = new ApexRegTest();

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.settings = Substitute.For<IFederationGatewaySettings>();
            this.opReturnDataReader = Substitute.For<IOpReturnDataReader>();
            this.withdrawalReceiver = Substitute.For<IWithdrawalReceiver>();

            this.addressHelper = new MultisigAddressHelper(this.network);

            this.settings.MultiSigAddress.Returns(this.addressHelper.TargetChainMultisigAddress);
            this.settings.MultiSigRedeemScript.Returns(this.addressHelper.PayToMultiSig);
            this.settings.FederationPublicKeys.Returns(this.addressHelper.MultisigPrivateKeys.Select(k => k.PubKey).ToArray());

            this.opReturnDataReader.TryGetTargetAddress(null).ReturnsForAnyArgs((string)null);

            this.transactionBuilder = new TestMultisigTransactionBuilder(this.addressHelper);

            this.withdrawalExtractor = new WithdrawalExtractor(
                this.loggerFactory, this.settings, this.opReturnDataReader, this.withdrawalReceiver, this.network);
        }

        [Fact]
        public void ExtractWithdrawalsFromBlock_Should_Find_Withdrawals_From_Multisig()
        {
            var block = this.network.Consensus.ConsensusFactory.CreateBlock();

            var (targetScript, opReturnDepositId, amount, validWithdrawalTransaction) = AddWithdrawalToBlock(block);

            var blockHeight = 3456;

            var withdrawals = this.withdrawalExtractor.ExtractWithdrawalsFromBlock(block, blockHeight);

            this.withdrawalReceiver.Received(1).ReceiveWithdrawals(Arg.Is(withdrawals));

            withdrawals.Count.Should().Be(1);
            this.VerifyWithdrawalData(
                withdrawals[0],
                amount,
                block,
                blockHeight,
                validWithdrawalTransaction,
                opReturnDepositId,
                targetScript);
        }

        [Fact]
        public void ExtractWithdrawalsFromBlock_Should_Handle_Transaction_with_no_inputs()
        {
            var block = this.network.Consensus.ConsensusFactory.CreateBlock();

            var noInputRandomTransaction = this.transactionBuilder.BuildTransaction(this.addressHelper.GetNewTargetChainPubKeyAddress());
            block.AddTransaction(noInputRandomTransaction);
            var noInputTransactionToMultisig = this.transactionBuilder.BuildTransaction(this.addressHelper.TargetChainMultisigAddress);
            block.AddTransaction(noInputTransactionToMultisig);

            var (targetScript, opReturnDepositId, amount, validWithdrawalTransaction) = AddWithdrawalToBlock(block);

            var blockHeight = 5972176;

            var withdrawals = this.withdrawalExtractor.ExtractWithdrawalsFromBlock(block, blockHeight);

            this.withdrawalReceiver.Received(1).ReceiveWithdrawals(Arg.Is(withdrawals));

            withdrawals.Count.Should().Be(1);
            this.VerifyWithdrawalData(
                withdrawals[0],
                amount,
                block,
                blockHeight,
                validWithdrawalTransaction,
                opReturnDepositId,
                targetScript);
        }

        [Fact]
        public void ExtractWithdrawalsFromBlock_Should_Only_Find_Withdrawals_From_Multisig()
        {
            var block = this.network.Consensus.ConsensusFactory.CreateBlock();

            var sender = new Key();
            var transactionOutOfMultisigButNoOpReturn = this.transactionBuilder.GetWithdrawalOutOfMultisigTo(
                this.addressHelper.GetNewTargetChainPaymentScript(),
                null);
            block.AddTransaction(transactionOutOfMultisigButNoOpReturn);

            var transactionOutOfMultisigWithInvalidOpReturn =
                this.transactionBuilder.GetWithdrawalOutOfMultisigTo(
                    this.addressHelper.GetNewTargetChainPaymentScript(),
                    Encoding.UTF8.GetBytes("not valid as uint256"));
            block.AddTransaction(transactionOutOfMultisigWithInvalidOpReturn);

            var standardTransactionInTargetChain = this.transactionBuilder.GetTransactionWithInputs(
                this.network, sender, this.addressHelper.GetNewTargetChainPaymentScript());
            block.AddTransaction(standardTransactionInTargetChain);

            var validOpReturn = TestingValues.GetUint256();
            var randomTransactionWithValidOpReturn = this.transactionBuilder.GetTransactionWithInputs(
                this.network, sender, this.addressHelper.GetNewTargetChainPaymentScript(), validOpReturn.ToBytes());
            block.AddTransaction(randomTransactionWithValidOpReturn);
            this.opReturnDataReader.TryGetTransactionId(randomTransactionWithValidOpReturn)
                .Returns(validOpReturn.ToString());

            var (targetScript, opReturnDepositId, amount, validWithdrawalTransaction) = AddWithdrawalToBlock(block);

            var blockHeight = 3456;
            var withdrawals = this.withdrawalExtractor.ExtractWithdrawalsFromBlock(block, blockHeight);

            withdrawals.Count.Should().Be(1);
            this.VerifyWithdrawalData(
                withdrawals[0],
                amount,
                block,
                blockHeight,
                validWithdrawalTransaction,
                opReturnDepositId,
                targetScript);

            this.withdrawalReceiver.Received(1).ReceiveWithdrawals(Arg.Is(withdrawals));
        }

        [Fact]
        public void ExtractWithdrawalsFromBlock_Should_Find_Multiple_Withdrawals_From_Multisig()
        {
            var block = this.network.Consensus.ConsensusFactory.CreateBlock();

            var (targetScript1, opReturnDepositId1, amount1, validWithdrawalTransaction1) = AddWithdrawalToBlock(block);
            var (targetScript2, opReturnDepositId2, amount2, validWithdrawalTransaction2) = AddWithdrawalToBlock(block);

            var blockHeight = 78931;
            var withdrawals = this.withdrawalExtractor.ExtractWithdrawalsFromBlock(block, blockHeight);

            withdrawals.Count.Should().Be(2);

            this.withdrawalReceiver.Received(1).ReceiveWithdrawals(Arg.Is(withdrawals));

            this.VerifyWithdrawalData(
                withdrawals[0],
                amount1,
                block,
                blockHeight,
                validWithdrawalTransaction1,
                opReturnDepositId1,
                targetScript1);

            this.VerifyWithdrawalData(
                withdrawals[1],
                amount2,
                block,
                blockHeight,
                validWithdrawalTransaction2,
                opReturnDepositId2,
                targetScript2);
        }

        private (Script targetScript, uint256 opReturnDepositId, long amount, Transaction validWithdrawalTransaction)
            AddWithdrawalToBlock(Block block)
        {
            var targetScript = this.addressHelper.GetNewTargetChainPaymentScript();
            var opReturnDepositId = TestingValues.GetUint256();
            long amount = 22 * Money.COIN;
            var validWithdrawalTransaction = this.transactionBuilder.GetWithdrawalOutOfMultisigTo(
                targetScript,
                opReturnDepositId.ToBytes(),
                amount,
                true);
            block.AddTransaction(validWithdrawalTransaction);
            this.opReturnDataReader.TryGetTransactionId(validWithdrawalTransaction).Returns(opReturnDepositId.ToString());
            return (targetScript, opReturnDepositId, amount, validWithdrawalTransaction);
        }

        private void VerifyWithdrawalData(
            IWithdrawal withdrawals,
            long amount,
            Block block,
            int blockHeight,
            Transaction validWithdrawalTransaction,
            uint256 opReturnDepositId,
            Script targetScript)
        {
            withdrawals.Amount.Satoshi.Should().Be(amount);
            withdrawals.BlockHash.Should().Be(block.Header.GetHash());
            withdrawals.BlockNumber.Should().Be(blockHeight);
            withdrawals.Id.Should().Be(validWithdrawalTransaction.GetHash());
            withdrawals.DepositId.Should().Be(opReturnDepositId);
            withdrawals.TargetAddress.Should().Be(targetScript.GetScriptAddress(this.network).ToString());
        }
    }
}
