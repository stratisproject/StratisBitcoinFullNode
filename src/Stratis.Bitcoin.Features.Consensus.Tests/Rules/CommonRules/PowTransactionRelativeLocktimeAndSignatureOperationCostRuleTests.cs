﻿using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.Consensus.Tests.CoinViews;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PowTransactionRelativeLocktimeAndSignatureOperationCostRuleTests
    {
        private const int HeightOfBlockchain = 1;
        private List<Transaction> transactions;
        private RuleContext ruleContext;
        private Exception caughtExecption;
        private Transaction transactionWithCoinbase;
        private UnspentOutputSet coinView;
        private Transaction transactionWithCoinbaseFromPreviousBlock;

        [Fact]
        public void RunAsync_ValidatingATransactionThatIsNotCoinBaseButStillHasUnspentOutputsWithoutInput_ThrowsBadTransactionMissingInput()
        {
            this.GivenACoinbaseTransactionFromAPreviousBlock();
            //this.GivenACoinbaseTransaction();
            this.AndARuleContext();
            this.AndSomeUnspentOutputs();
            this.AndATransactionWithNoUnspentOutputsAsInput();
            this.WhenExecutingTheRule();
            this.ThenExceptionThrownIs(ConsensusErrors.BadTransactionMissingInput);
        }
          
        [Fact]
        public void RunAsync_ValidatingABlockHeightLowerThanBIP86Allows_ThrowsBadTransactionNonFinal()
        {
            this.GivenACoinbaseTransactionFromAPreviousBlock();
            //this.GivenACoinbaseTransaction();
            this.AndARuleContext();
            this.AndSomeUnspentOutputs();
            this.AndATransactionBlockHeightLowerThanBip68Allows();
            this.WhenExecutingTheRule();
            this.ThenExceptionThrownIs(ConsensusErrors.BadTransactionNonFinal);
        }

        //[Fact]
        public void RunAsync_AttemptingABlockEarlierThanBIP86Allows_ThrowsBadTransactionNonFinal()
        {
            //TODO before PR complete - Similar test to the block height but considering the time not the height.
        }

        private void AndSomeUnspentOutputs()
        {
            this.coinView = new UnspentOutputSet();
            this.coinView.SetCoins(new UnspentOutputs[0]);
            this.ruleContext.Set = this.coinView;
            this.coinView.Update(this.transactionWithCoinbaseFromPreviousBlock, 0);
        }

        private void AndARuleContext()
        { 
            this.ruleContext = new RuleContext { };
            this.ruleContext.BlockValidationContext = new BlockValidationContext();
            this.coinView = new UnspentOutputSet();
            this.ruleContext.Set = this.coinView;

            this.transactions = new List<Transaction>();
            this.ruleContext.BlockValidationContext.Block = new Block()
            {
                Transactions = this.transactions
            };
            this.ruleContext.BlockValidationContext.ChainedBlock = new ChainedBlock(new BlockHeader(), new uint256("bcd7d5de8d3bcc7b15e7c8e5fe77c0227cdfa6c682ca13dcf4910616f10fdd06"), HeightOfBlockchain);
            this.ruleContext.Flags = new DeploymentFlags();

            //TODO: COINBASE TRANSACTION REMOVED FROM TEST AT THE MOMENT - SO CANT BE ADDED HERE UNLESS NOT NULL
            if (this.transactionWithCoinbase != null)
                this.transactions.Add(this.transactionWithCoinbase);
        }

        private void AndATransactionBlockHeightLowerThanBip68Allows()
        {
            var transaction = new Transaction
            {
                Inputs = { new TxIn()
                {
                    PrevOut = new OutPoint(this.transactionWithCoinbaseFromPreviousBlock, 0),
                    Sequence = HeightOfBlockchain + 1, //this sequence being higher triggers the ThrowsBadTransactionNonFinal
                } },
                Outputs = { new TxOut()},
                Version = 2, // So that sequence locks considered (BIP68)
            };

            this.ruleContext.Flags = new DeploymentFlags() { LockTimeFlags = Transaction.LockTimeFlags.VerifySequence };
            this.transactions.Add(transaction);
        }

        private void GivenACoinbaseTransaction()
        {
            this.transactionWithCoinbase = new Transaction();
            var txIn = new TxIn { PrevOut = new OutPoint() };
            this.transactionWithCoinbase.AddInput(txIn);
            this.transactionWithCoinbase.AddOutput(new TxOut());
        }

        private void GivenACoinbaseTransactionFromAPreviousBlock()
        {
            this.transactionWithCoinbaseFromPreviousBlock = new Transaction();
            var txIn = new TxIn { PrevOut = new OutPoint() };
            this.transactionWithCoinbaseFromPreviousBlock.AddInput(txIn);
            this.transactionWithCoinbaseFromPreviousBlock.AddOutput(new TxOut());
        }

        private void AndATransactionWithNoUnspentOutputsAsInput()
        {
            this.transactions.Add(new Transaction { Inputs = { new TxIn() } });
        }

        private void WhenExecutingTheRule()
        {
            try
            {
                var rule = new PowTransactionRelativeLocktimeAndSignatureOperationCostRule()
                {
                    Logger = new Mock<ILogger>().Object,
                    Parent = new PowConsensusRules(Network.RegTest, new Mock<ILoggerFactory>().Object, new Mock<IDateTimeProvider>().Object, new ConcurrentChain(), new NodeDeployments(Network.RegTest, new ConcurrentChain()), new ConsensusSettings(), new Mock<ICheckpoints>().Object, new Mock<CoinView>().Object, null)
                };
                rule.Initialize();

                rule.RunAsync(this.ruleContext).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                this.caughtExecption = e;
            }
        }

        private void ThenExceptionThrownIs(ConsensusError consensusErrorType)
        {
            this.caughtExecption.Should().NotBeNull();
            this.caughtExecption.Should().BeOfType<ConsensusErrorException>();
            var consensusErrorException = (ConsensusErrorException) this.caughtExecption;
            consensusErrorException.ConsensusError.Should().Be(consensusErrorType);
        }
    }
}