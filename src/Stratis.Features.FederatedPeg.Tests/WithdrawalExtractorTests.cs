using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class WithdrawalExtractorTests
    {
        private readonly IFederationGatewaySettings settings;

        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILoggerFactory loggerFactory;

        private IWithdrawalReceiver withdrawalReceiver;

        private WithdrawalExtractor withdrawalExtractor;

        private readonly Network network;

        private readonly MultisigAddressHelper addressHelper;

        private readonly TestMultisigTransactionBuilder transactionBuilder;

        public WithdrawalExtractorTests()
        {
            this.network = FederatedPegNetwork.NetworksSelector.Regtest();

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.settings = Substitute.For<IFederationGatewaySettings>();
            this.opReturnDataReader = Substitute.For<IOpReturnDataReader>();
            this.withdrawalReceiver = Substitute.For<IWithdrawalReceiver>();

            this.addressHelper = new MultisigAddressHelper(this.network);

            this.settings.MultiSigAddress.Returns(this.addressHelper.TargetChainMultisigAddress);
            this.settings.MultiSigRedeemScript.Returns(this.addressHelper.PayToMultiSig);
            this.settings.FederationPublicKeys.Returns(this.addressHelper.MultisigPrivateKeys.Select(k => k.PubKey).ToArray());

            this.opReturnDataReader.TryGetTargetAddress(null, out string address).Returns(callInfo => { callInfo[1] = null; return false; });

            this.transactionBuilder = new TestMultisigTransactionBuilder(this.addressHelper);

            this.withdrawalExtractor = new WithdrawalExtractor(
                this.loggerFactory, this.settings, this.opReturnDataReader, this.network);
        }

        // TODO: Will depend on decision made on backlog issue https://github.com/stratisproject/FederatedSidechains/issues/124
        /*
        [Fact(Skip = TestingValues.SkipTests)]
        public void ExtractWithdrawalsFromBlock_Should_Find_Withdrawals_From_Multisig()
        {
            var block = this.network.Consensus.ConsensusFactory.CreateBlock();

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
        }

        [Fact(Skip = TestingValues.SkipTests)]
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

        [Fact(Skip = TestingValues.SkipTests)]
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
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void ExtractWithdrawalsFromBlock_Should_Find_Multiple_Withdrawals_From_Multisig()
        {
            var block = this.network.Consensus.ConsensusFactory.CreateBlock();

            var (targetScript1, opReturnDepositId1, amount1, validWithdrawalTransaction1) = AddWithdrawalToBlock(block);
            var (targetScript2, opReturnDepositId2, amount2, validWithdrawalTransaction2) = AddWithdrawalToBlock(block);

            var blockHeight = 78931;
            var withdrawals = this.withdrawalExtractor.ExtractWithdrawalsFromBlock(block, blockHeight);

            withdrawals.Count.Should().Be(2);

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
        */

        private (Script targetScript, uint256 opReturnDepositId, long amount, Transaction validWithdrawalTransaction)
            AddWithdrawalToBlock(Block block)
        {
            Script targetScript = this.addressHelper.GetNewTargetChainPaymentScript();
            uint256 opReturnDepositId = TestingValues.GetUint256();
            long amount = 22 * Money.COIN;
            Transaction validWithdrawalTransaction = this.transactionBuilder.GetWithdrawalOutOfMultisigTo(
                targetScript,
                opReturnDepositId.ToBytes(),
                amount,
                true);

            block.AddTransaction(validWithdrawalTransaction);
            this.opReturnDataReader.TryGetTransactionId(validWithdrawalTransaction, out string txId).Returns(callInfo => { callInfo[1] = opReturnDepositId.ToString(); return true; });

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
