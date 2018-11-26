using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.SmartContracts.PoW;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Executor.Reflection;
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
            var contractState = new StateRepositoryRoot();
            var executorFactory = new Mock<IContractExecutorFactory>();
            var loggerFactory = new ExtendedLoggerFactory();

            var dateTimeProvider = new DateTimeProvider();
            var callDataSerializer = Mock.Of<ICallDataSerializer>();

            var consensusRules = new SmartContractPowConsensusRuleEngine(
                chain, new Mock<ICheckpoints>().Object, new Configuration.Settings.ConsensusSettings(NodeSettings.Default(network)),
                DateTimeProvider.Default, executorFactory.Object, loggerFactory, network,
                new Base.Deployments.NodeDeployments(network, chain), contractState,
                new Mock<IReceiptRepository>().Object,
                new Mock<ISenderRetriever>().Object,
                new Mock<ICoinView>().Object,
                new Mock<IChainState>().Object,
                new InvalidBlockHashStore(dateTimeProvider),
                new NodeStats(dateTimeProvider));

            var feature = new ReflectionVirtualMachineFeature(loggerFactory, network, callDataSerializer);
            feature.InitializeAsync().GetAwaiter().GetResult();

            Assert.Single(network.Consensus.FullValidationRules.Where(r => r.GetType() == typeof(SmartContractFormatRule)));
        }
    }
}