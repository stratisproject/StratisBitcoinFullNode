using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool.Rules;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests.Rules
{
    public sealed class CheckTxTotalOutVsFeeRuleTests
    {
        private readonly ChainIndexer chainIndexer;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly NodeSettings nodeSettings;
        private readonly ITxMempool txMempool;

        public CheckTxTotalOutVsFeeRuleTests()
        {
            this.network = new StratisMain();
            this.chainIndexer = new ChainIndexer(this.network);
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.nodeSettings = NodeSettings.Default(this.network);
            this.txMempool = new Mock<ITxMempool>().Object;
        }

        [Fact]
        public void CheckTxTotalOutVsFeeRule_Pass()
        {
            var rule = new CheckTxTotalOutVsFeeRule(this.network, this.txMempool, new MempoolSettings(this.nodeSettings), this.chainIndexer, this.loggerFactory);
            var transaction = CreateTransaction(Money.Coins(1));
            var mempoolValidationContext = new MempoolValidationContext(transaction, new MempoolValidationState(false))
            {
                MinRelayTxFee = this.nodeSettings.MinRelayTxFeeRate,
                ValueOut = transaction.TotalOut
            };

            rule.CheckTransaction(mempoolValidationContext);
            Assert.Null(mempoolValidationContext.State.Error);
        }

        [Fact]
        public void CheckTxTotalOutVsFeeRule_Fail()
        {
            var rule = new CheckTxTotalOutVsFeeRule(this.network, this.txMempool, new MempoolSettings(this.nodeSettings), this.chainIndexer, this.loggerFactory);
            var transaction = CreateTransaction(Money.Coins(0.000001m));
            var mempoolValidationContext = new MempoolValidationContext(transaction, new MempoolValidationState(false))
            {
                MinRelayTxFee = this.nodeSettings.MinRelayTxFeeRate,
                ValueOut = transaction.TotalOut
            };

            Assert.Throws<MempoolErrorException>(() => rule.CheckTransaction(mempoolValidationContext));
            Assert.NotNull(mempoolValidationContext.State.Error);
            Assert.Equal(MempoolErrors.TxTotalOutLessThanMinRelayFee, mempoolValidationContext.State.Error);
        }

        private Transaction CreateTransaction(Money amount)
        {
            var transaction = this.network.CreateTransaction();
            transaction.AddInput(TxIn.CreateCoinbase(1));
            transaction.AddOutput(new TxOut(amount, new Script()));
            return transaction;
        }
    }
}
