using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class PoATestsBase
    {
        protected readonly ChainedHeader currentHeader;
        protected readonly TestPoANetwork network;
        protected readonly PoAConsensusOptions consensusOptions;

        protected PoAConsensusRuleEngine rulesEngine;
        protected readonly LoggerFactory loggerFactory;
        protected readonly PoABlockHeaderValidator poaHeaderValidator;
        protected readonly SlotsManager slotsManager;
        protected readonly ConsensusSettings consensusSettings;
        protected readonly ConcurrentChain chain;
        protected readonly FederationManager federationManager;
        protected readonly VotingManager votingManager;
        protected readonly Mock<IPollResultExecutor> resultExecutorMock;
        protected readonly ISignals signals;
        protected readonly DBreezeSerializer dBreezeSerializer;
        protected readonly ChainState chainState;

        public PoATestsBase(TestPoANetwork network = null)
        {
            this.signals = new Signals.Signals();
            this.loggerFactory = new LoggerFactory();
            this.network = network == null ? new TestPoANetwork() : network;
            this.consensusOptions = this.network.ConsensusOptions;
            this.dBreezeSerializer = new DBreezeSerializer(this.network);

            this.chain = new ConcurrentChain(this.network);
            IDateTimeProvider timeProvider = new DateTimeProvider();
            this.consensusSettings = new ConsensusSettings(NodeSettings.Default(this.network));

            this.federationManager = CreateFederationManager(this, this.network, this.loggerFactory);
            this.slotsManager = new SlotsManager(this.network, this.federationManager, this.loggerFactory);

            this.poaHeaderValidator = new PoABlockHeaderValidator(this.loggerFactory);

            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            var finalizedBlockRepo = new FinalizedBlockInfoRepository(new KeyValueRepository(dataFolder, this.dBreezeSerializer), this.loggerFactory);
            finalizedBlockRepo.LoadFinalizedBlockInfoAsync(this.network).GetAwaiter().GetResult();

            this.resultExecutorMock = new Mock<IPollResultExecutor>();

            this.votingManager = new VotingManager(this.federationManager, this.loggerFactory, this.slotsManager, this.resultExecutorMock.Object, new NodeStats(timeProvider),
                 dataFolder, this.dBreezeSerializer, this.signals, finalizedBlockRepo);

            this.votingManager.Initialize();

            this.chainState = new ChainState();

            this.rulesEngine = new PoAConsensusRuleEngine(this.network, this.loggerFactory, new DateTimeProvider(), this.chain, new NodeDeployments(this.network, this.chain),
                this.consensusSettings, new Checkpoints(this.network, this.consensusSettings), new Mock<ICoinView>().Object, this.chainState, new InvalidBlockHashStore(timeProvider),
                new NodeStats(timeProvider), this.slotsManager, this.poaHeaderValidator, this.votingManager, this.federationManager);

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(50, null, false, null, this.network);

            this.currentHeader = headers.Last();
        }

        public static FederationManager CreateFederationManager(object caller, Network network, LoggerFactory loggerFactory)
        {
            string dir = TestBase.CreateTestDir(caller);
            var keyValueRepo = new KeyValueRepository(dir, new DBreezeSerializer(network));

            var settings = new NodeSettings(network, args: new string[] { $"-datadir={dir}" });
            var federationManager = new FederationManager(settings, network, loggerFactory, keyValueRepo);
            federationManager.Initialize();

            return federationManager;
        }

        public static FederationManager CreateFederationManager(object caller)
        {
            return CreateFederationManager(caller, new TestPoANetwork(), new ExtendedLoggerFactory());
        }

        public void InitRule(ConsensusRuleBase rule)
        {
            rule.Parent = this.rulesEngine;
            rule.Logger = this.loggerFactory.CreateLogger(rule.GetType().FullName);
            rule.Initialize();
        }
    }

    public class TestPoANetwork : PoANetwork
    {
        public TestPoANetwork(List<PubKey> pubKeysOverride = null)
        {
            var federationPublicKeys = new List<PubKey>()
            {
                new PubKey("02d485fc5ae101c2780ff5e1f0cb92dd907053266f7cf3388eb22c5a4bd266ca2e"),
                new PubKey("026ed3f57de73956219b85ef1e91b3b93719e2645f6e804da4b3d1556b44a477ef"),
                new PubKey("03895a5ba998896e688b7d46dd424809b0362d61914e1432e265d9539fe0c3cac0"),
                new PubKey("020fc3b6ac4128482268d96f3bd911d0d0bf8677b808eaacd39ecdcec3af66db34"),
                new PubKey("038d196fc2e60d6dfc533c6a905ba1f9092309762d8ebde4407d209e37a820e462"),
                new PubKey("0358711f76435a508d98a9dee2a7e160fed5b214d97e65ea442f8f1265d09e6b55")
            };

            if (pubKeysOverride != null)
                federationPublicKeys = pubKeysOverride;

            var baseOptions = this.Consensus.Options as PoAConsensusOptions;

            this.Consensus.Options = new PoAConsensusOptions(
                maxBlockBaseSize: baseOptions.MaxBlockBaseSize,
                maxStandardVersion: baseOptions.MaxStandardVersion,
                maxStandardTxWeight: baseOptions.MaxStandardTxWeight,
                maxBlockSigopsCost: baseOptions.MaxBlockSigopsCost,
                maxStandardTxSigopsCost: baseOptions.MaxStandardTxSigopsCost,
                federationPublicKeys: federationPublicKeys,
                targetSpacingSeconds: 60,
                votingEnabled: baseOptions.VotingEnabled
            );

            this.Consensus.SetPrivatePropertyValue(nameof(this.Consensus.MaxReorgLength), (uint)5);
        }
    }
}
