﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Networks;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.SourceChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class DepositExtractorTests
    {
        private readonly IFederationGatewaySettings settings;

        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILoggerFactory loggerFactory;

        private readonly DepositExtractor depositExtractor;

        private readonly Network network;

        private readonly Network counterChainNetwork;

        private readonly MultisigAddressHelper addressHelper;

        private readonly TestTransactionBuilder transactionBuilder;

        private readonly ChainIndexer chainIndexer;

        public DepositExtractorTests()
        {
            this.network = FederatedPegNetwork.NetworksSelector.Regtest();
            this.counterChainNetwork = Networks.Stratis.Regtest();

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.settings = Substitute.For<IFederationGatewaySettings>();
            this.opReturnDataReader = Substitute.For<IOpReturnDataReader>();

            this.addressHelper = new MultisigAddressHelper(this.network, this.counterChainNetwork);

            this.settings.MultiSigRedeemScript.Returns(this.addressHelper.PayToMultiSig);
            this.settings.TransactionFee.Returns(FederationGatewaySettings.DefaultTransactionFee);
            this.opReturnDataReader.TryGetTargetAddress(null, out string address).Returns(callInfo => { callInfo[1] = null; return false; });

            this.transactionBuilder = new TestTransactionBuilder();

            this.depositExtractor = new DepositExtractor(
                this.loggerFactory,
                this.settings,
                this.opReturnDataReader);
        }

        [Fact]
        public void ExtractDepositsFromBlock_Should_Only_Find_Deposits_To_Multisig()
        {
            Block block = this.network.Consensus.ConsensusFactory.CreateBlock();

            BitcoinPubKeyAddress targetAddress = this.addressHelper.GetNewTargetChainPubKeyAddress();
            byte[] opReturnBytes = Encoding.UTF8.GetBytes(targetAddress.ToString());
            long depositAmount = Money.COIN * 3;
            Transaction depositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, opReturnBytes, depositAmount);
            block.AddTransaction(depositTransaction);

            this.opReturnDataReader.TryGetTargetAddress(depositTransaction, out string address).Returns(callInfo => { callInfo[1] = targetAddress.ToString(); return true; });

            Transaction nonDepositTransactionToMultisig = this.transactionBuilder.BuildTransaction(
                this.addressHelper.SourceChainMultisigAddress);
            block.AddTransaction(nonDepositTransactionToMultisig);

            BitcoinPubKeyAddress otherAddress = this.addressHelper.GetNewSourceChainPubKeyAddress();
            otherAddress.ToString().Should().NotBe(this.addressHelper.SourceChainMultisigAddress.ToString(),
                "otherwise the next deposit should actually be extracted");
            Transaction depositTransactionToOtherAddress =
                this.transactionBuilder.BuildOpReturnTransaction(otherAddress, opReturnBytes);
            block.AddTransaction(depositTransactionToOtherAddress);

            Transaction nonDepositTransactionToOtherAddress = this.transactionBuilder.BuildTransaction(
                otherAddress);
            block.AddTransaction(nonDepositTransactionToOtherAddress);

            int blockHeight = 230;
            IReadOnlyList<IDeposit> extractedDeposits = this.depositExtractor.ExtractDepositsFromBlock(block, blockHeight);

            extractedDeposits.Count.Should().Be(1);
            IDeposit extractedTransaction = extractedDeposits[0];

            extractedTransaction.Amount.Satoshi.Should().Be(depositAmount);
            extractedTransaction.Id.Should().Be(depositTransaction.GetHash());
            extractedTransaction.TargetAddress.Should().Be(targetAddress.ToString());
            extractedTransaction.BlockNumber.Should().Be(blockHeight);
            extractedTransaction.BlockHash.Should().Be(block.GetHash());
        }

        [Fact]
        public void ExtractDepositsFromBlock_Should_Create_One_Deposit_Per_Transaction_To_Multisig()
        {
            Block block = this.network.Consensus.ConsensusFactory.CreateBlock();

            BitcoinPubKeyAddress targetAddress = this.addressHelper.GetNewTargetChainPubKeyAddress();
            byte[] opReturnBytes = Encoding.UTF8.GetBytes(targetAddress.ToString());
            long depositAmount = Money.COIN * 3;
            Transaction depositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, opReturnBytes, depositAmount);
            block.AddTransaction(depositTransaction);

            this.opReturnDataReader.TryGetTargetAddress(depositTransaction, out string unused1).Returns(callInfo => { callInfo[1] = targetAddress.ToString(); return true; });

            //add another deposit to the same address
            long secondDepositAmount = Money.COIN * 2;
            Transaction secondDepositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, opReturnBytes, secondDepositAmount);
            block.AddTransaction(secondDepositTransaction);
            this.opReturnDataReader.TryGetTargetAddress(secondDepositTransaction, out string unused2).Returns(callInfo => { callInfo[1] = targetAddress.ToString(); return true; });

            //add another deposit to a different address
            string newTargetAddress = this.addressHelper.GetNewTargetChainPubKeyAddress().ToString();
            byte[] newOpReturnBytes = Encoding.UTF8.GetBytes(newTargetAddress);
            long thirdDepositAmount = Money.COIN * 34;
            Transaction thirdDepositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, newOpReturnBytes, thirdDepositAmount);
            block.AddTransaction(thirdDepositTransaction);

            this.opReturnDataReader.TryGetTargetAddress(thirdDepositTransaction, out string unused3).Returns(callInfo => { callInfo[1] = newTargetAddress; return true; });

            int blockHeight = 12345;
            IReadOnlyList<IDeposit> extractedDeposits = this.depositExtractor.ExtractDepositsFromBlock(block, blockHeight);

            extractedDeposits.Count.Should().Be(3);
            extractedDeposits.Select(d => d.BlockNumber).Should().AllBeEquivalentTo(blockHeight);
            extractedDeposits.Select(d => d.BlockHash).Should().AllBeEquivalentTo(block.GetHash());

            IDeposit extractedTransaction = extractedDeposits[0];
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

        [Fact]
        public void ExtractDepositsFromBlockShouldOnlyGetAmountsGreaterThanWithdrawalFee()
        {
            Block block = this.network.Consensus.ConsensusFactory.CreateBlock();

            BitcoinPubKeyAddress targetAddress = this.addressHelper.GetNewTargetChainPubKeyAddress();
            byte[] opReturnBytes = Encoding.UTF8.GetBytes(targetAddress.ToString());

            // Set amount to be less than withdrawal fee
            long depositAmount = FederationGatewaySettings.DefaultTransactionFee - 1;
            Transaction depositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, opReturnBytes, depositAmount);
            block.AddTransaction(depositTransaction);
            this.opReturnDataReader.TryGetTargetAddress(depositTransaction, out string unused1).Returns(callInfo => { callInfo[1] = targetAddress.ToString(); return true; });

            // Set amount to be exactly withdrawal fee
            long secondDepositAmount = FederationGatewaySettings.DefaultTransactionFee;
            Transaction secondDepositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, opReturnBytes, secondDepositAmount);
            block.AddTransaction(secondDepositTransaction);
            this.opReturnDataReader.TryGetTargetAddress(secondDepositTransaction, out string unused2).Returns(callInfo => { callInfo[1] = targetAddress.ToString(); return true; });

            // Set amount to be greater than withdrawal fee (just)
            long thirdDepositAmount = FederationGatewaySettings.DefaultTransactionFee + 1;
            Transaction thirdDepositTransaction = this.transactionBuilder.BuildOpReturnTransaction(
                this.addressHelper.SourceChainMultisigAddress, opReturnBytes, thirdDepositAmount);
            block.AddTransaction(thirdDepositTransaction);
            this.opReturnDataReader.TryGetTargetAddress(thirdDepositTransaction, out string unused3).Returns(callInfo => { callInfo[1] = targetAddress.ToString(); return true; });// Extract deposits
            int blockHeight = 12345;
            IReadOnlyList<IDeposit> extractedDeposits = this.depositExtractor.ExtractDepositsFromBlock(block, blockHeight);

            // Should only be one, with the value just over the withdrawal fee.
            extractedDeposits.Count.Should().Be(1);
            extractedDeposits.First().Amount.Should().Be(FederationGatewaySettings.DefaultTransactionFee + 1);
        }
    }
}
