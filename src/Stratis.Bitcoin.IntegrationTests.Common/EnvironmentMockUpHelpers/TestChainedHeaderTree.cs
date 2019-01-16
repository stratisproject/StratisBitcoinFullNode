using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    /// <summary>Test-only implementation of <see cref="TestChainedHeaderTree"/> that exposes inner structures.</summary>
    internal class TestChainedHeaderTree : ChainedHeaderTree
    {
        public TestChainedHeaderTree(
            Network network,
            ILoggerFactory loggerFactory,
            IHeaderValidator headerValidator,
            ICheckpoints checkpoints,
            IChainState chainState,
            IFinalizedBlockInfoRepository finalizedBlockInfo,
            ConsensusSettings consensusSettings,
            IInvalidBlockHashStore invalidHashesStore) : base(network, loggerFactory, headerValidator, checkpoints,
                chainState, finalizedBlockInfo, consensusSettings, invalidHashesStore)
        {
        }

        public Dictionary<uint256, HashSet<int>> PeerIdsByTipHash => typeof(ChainedHeaderTree).GetField("peerIdsByTipHash", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this) as Dictionary<uint256, HashSet<int>>;

        public Dictionary<int, uint256> PeerTipsByPeerId => typeof(ChainedHeaderTree).GetField("peerTipsByPeerId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this) as Dictionary<int, uint256>;

        public Dictionary<uint256, ChainedHeader> ChainedHeadersByHash => typeof(ChainedHeaderTree).GetField("chainedHeadersByHash", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this) as Dictionary<uint256, ChainedHeader>;
    }
}
