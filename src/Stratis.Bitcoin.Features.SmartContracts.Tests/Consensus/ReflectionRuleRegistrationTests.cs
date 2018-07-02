using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus
{
    public sealed class ReflectionRuleRegistrationTests
    {
        [Fact]
        public void ReflectionVirtualMachineFeature_OnInitialize_RulesAdded()
        {
            var chain = new ConcurrentChain();
            var loggerFactory = new ExtendedLoggerFactory();
            Network network = Network.StratisRegTest;

            var consensusRules = new SmartContractConsensusRules(network, loggerFactory, DateTimeProvider.Default, chain, new Base.Deployments.NodeDeployments(network, chain), new Configuration.Settings.ConsensusSettings(), new Mock<ICheckpoints>().Object, new Mock<CoinView>().Object, new Mock<ILookaheadBlockPuller>().Object);
            var feature = new ReflectionVirtualMachineFeature(consensusRules, loggerFactory);
            feature.Initialize();

            Assert.Single(consensusRules.Rules.Where(r => r.Rule.GetType() == typeof(SmartContractFormatRule)));
        }
    }
}