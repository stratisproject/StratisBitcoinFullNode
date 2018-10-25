using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class InitialBlockDownloadStateMock : IInitialBlockDownloadState
    {
        public InitialBlockDownloadStateMock(IChainState chainState, Network network, ConsensusSettings consensusSettings, ICheckpoints checkpoints)
        {
        }

        public bool IsInitialBlockDownload()
        {
            return false;
        }
    }
}
