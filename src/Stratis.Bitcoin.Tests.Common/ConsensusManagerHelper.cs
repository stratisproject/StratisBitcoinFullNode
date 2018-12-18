using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Tests.Common
{
    public static class ConsensusManagerHelper
    {
        public static ConsensusManager CreateConsensusManager(
            Network network,
            string dataDir = null,
            ChainState chainState = null,
            InMemoryCoinView inMemoryCoinView = null,
            ConcurrentChain chain = null,
            IRuleRegistration ruleRegistration = null,
            ConsensusRuleEngine consensusRules = null)
        {
            string[] param = dataDir == null ? new string[] { } : new string[] { $"-datadir={dataDir}" };

            var nodeSettings = new NodeSettings(network, args: param);

            ILoggerFactory loggerFactory = nodeSettings.LoggerFactory;
            IDateTimeProvider dateTimeProvider = DateTimeProvider.Default;

            network.Consensus.Options = new ConsensusOptions();

            if (ruleRegistration == null)
                ruleRegistration = new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration();

            ruleRegistration.RegisterRules(network.Consensus);

            // Dont check PoW of a header in this test.
            network.Consensus.HeaderValidationRules.RemoveAll(x => x.GetType() == typeof(CheckDifficultyPowRule));

            var consensusSettings = new ConsensusSettings(nodeSettings);

            if (chain == null)
                chain = new ConcurrentChain(network);

            if (inMemoryCoinView == null)
                inMemoryCoinView = new InMemoryCoinView(chain.Tip.HashBlock);

            var networkPeerFactory = new NetworkPeerFactory(network,
                dateTimeProvider,
                loggerFactory, new PayloadProvider().DiscoverPayloads(),
                new SelfEndpointTracker(loggerFactory),
                new Mock<IInitialBlockDownloadState>().Object,
                new ConnectionManagerSettings(nodeSettings));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, nodeSettings.DataFolder, loggerFactory, new SelfEndpointTracker(loggerFactory));
            var peerDiscovery = new PeerDiscovery(new AsyncLoopFactory(loggerFactory), loggerFactory, network, networkPeerFactory, new NodeLifetime(), nodeSettings, peerAddressManager);
            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var selfEndpointTracker = new SelfEndpointTracker(loggerFactory);
            var connectionManager = new ConnectionManager(dateTimeProvider, loggerFactory, network, networkPeerFactory, nodeSettings,
                new NodeLifetime(), new NetworkPeerConnectionParameters(), peerAddressManager, new IPeerConnector[] { },
                peerDiscovery, selfEndpointTracker, connectionSettings, new VersionProvider(), new Mock<INodeStats>().Object);

            if (chainState == null)
                chainState = new ChainState();
            var peerBanning = new PeerBanning(connectionManager, loggerFactory, dateTimeProvider, peerAddressManager);
            var deployments = new NodeDeployments(network, chain);

            if (consensusRules == null)
            {
                consensusRules = new PowConsensusRuleEngine(network, loggerFactory, dateTimeProvider, chain, deployments, consensusSettings,
                    new Checkpoints(), inMemoryCoinView, chainState, new InvalidBlockHashStore(dateTimeProvider), new NodeStats(dateTimeProvider)).Register();
            }

            consensusRules.Register();

            var tree = new ChainedHeaderTree(network, loggerFactory, new HeaderValidator(consensusRules, loggerFactory), new Checkpoints(),
                new ChainState(), new Mock<IFinalizedBlockInfoRepository>().Object, consensusSettings, new InvalidBlockHashStore(new DateTimeProvider()));

            var consensus = new ConsensusManager(tree, network, loggerFactory, chainState, new IntegrityValidator(consensusRules, loggerFactory),
                new PartialValidator(consensusRules, loggerFactory), new FullValidator(consensusRules, loggerFactory), consensusRules,
                new Mock<IFinalizedBlockInfoRepository>().Object, new Signals.Signals(), peerBanning, new Mock<IInitialBlockDownloadState>().Object, chain,
                new Mock<IBlockPuller>().Object, new Mock<IBlockStore>().Object, new Mock<IConnectionManager>().Object, new Mock<INodeStats>().Object, new NodeLifetime(), consensusSettings);

            return consensus;
        }
    }
}
