using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus
{
    public sealed class ReflectionRuleRegistrationTests
    {
        [Fact]
        public void ReflectionVirtualMachineFeature_OnInitialize_RulesAdded()
        {
            Network network = KnownNetworks.StratisRegTest;

            var chain = new ConcurrentChain(network);
            var contractState = new ContractStateRoot();
            var executorFactory = new Mock<ISmartContractExecutorFactory>();
            var loggerFactory = new ExtendedLoggerFactory();

            var consensusRules = new SmartContractPowConsensusRuleEngine(
                chain, new Mock<ICheckpoints>().Object, new Configuration.Settings.ConsensusSettings(),
                DateTimeProvider.Default, executorFactory.Object, loggerFactory, network,
                new Base.Deployments.NodeDeployments(network, chain), contractState,
                new Mock<ILookaheadBlockPuller>().Object,
                new Mock<IReceiptRepository>().Object,
                new Mock<ISenderRetriever>().Object,
                new Mock<ICoinView>().Object);

            var feature = new ReflectionVirtualMachineFeature(loggerFactory, network);
            feature.Initialize();

            Assert.Single(network.Consensus.Rules.Where(r => r.GetType() == typeof(SmartContractFormatRule)));
        }
    }
}