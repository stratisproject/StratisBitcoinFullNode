using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Networks;
using Xunit;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class CanGetSenderRuleTest
    {
        private readonly Network network;
        private readonly CanGetSenderRule rule;
        private readonly Mock<ISenderRetriever> senderRetriever;

        public CanGetSenderRuleTest()
        {
            this.network = new SmartContractsRegTest();
            this.senderRetriever = new Mock<ISenderRetriever>();
            this.rule = new CanGetSenderRule(this.senderRetriever.Object);
            this.rule.Parent = new PowConsensusRuleEngine(
                this.network,
                new Mock<ILoggerFactory>().Object,
                new Mock<IDateTimeProvider>().Object,
                new ConcurrentChain(this.network),
                new NodeDeployments(KnownNetworks.RegTest, new ConcurrentChain(this.network)),
                new ConsensusSettings(NodeSettings.Default(this.network)), new Mock<ICheckpoints>().Object, new Mock<ICoinView>().Object, new Mock<IChainState>().Object,
                new InvalidBlockHashStore(null),
                new NodeStats(null));

            this.rule.Initialize();
        }

        [Fact]
        public void P2PKH_GetSender_Passes()
        {
            var successResult = GetSenderResult.CreateSuccess(new uint160(0));
            this.senderRetriever.Setup(x => x.GetSender(It.IsAny<Transaction>(), It.IsAny<MempoolCoinView>()))
                .Returns(successResult);
            this.senderRetriever.Setup(x=> x.GetSender(It.IsAny<Transaction>(), It.IsAny<ICoinView>(), It.IsAny<IList<Transaction>>()))
                .Returns(successResult);

            Transaction transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(100, new Script(new byte[]{ (byte) ScOpcodeType.OP_CREATECONTRACT})));

            // Mempool check works
            this.rule.CheckTransaction(new MempoolValidationContext(transaction, new MempoolValidationState(false)));

            // Block validation check works
            Block block = this.network.CreateBlock();
            block.AddTransaction(transaction);
            this.rule.RunAsync(new RuleContext(new ValidationContext {BlockToValidate = block}, DateTimeOffset.Now));
        }

        [Fact]
        public void P2PKH_GetSender_Fails()
        {
            var failResult = GetSenderResult.CreateFailure("String error");
            this.senderRetriever.Setup(x => x.GetSender(It.IsAny<Transaction>(), It.IsAny<MempoolCoinView>()))
                .Returns(failResult);
            this.senderRetriever.Setup(x => x.GetSender(It.IsAny<Transaction>(), It.IsAny<ICoinView>(), It.IsAny<IList<Transaction>>()))
                .Returns(failResult);

            Transaction transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(100, new Script(new byte[] { (byte)ScOpcodeType.OP_CREATECONTRACT })));

            // Mempool check fails
            Assert.ThrowsAny<ConsensusErrorException>(() => this.rule.CheckTransaction(new MempoolValidationContext(transaction, new MempoolValidationState(false))));

            // Block validation check fails
            Block block = this.network.CreateBlock();
            block.AddTransaction(transaction);
            Assert.ThrowsAnyAsync<ConsensusErrorException>(() => this.rule.RunAsync(new RuleContext(new ValidationContext { BlockToValidate = block }, DateTimeOffset.Now)));
        }
    }
}
