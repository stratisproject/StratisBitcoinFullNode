using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class BestChainSelectorTest
    {
        private readonly Mock<IChainState> chainState;
        private readonly List<ChainedHeader> chainedHeaders;
        private readonly Network network;

        public BestChainSelectorTest()
        {
            this.chainedHeaders = new List<ChainedHeader>();
            var chain = new ConcurrentChain(Network.StratisMain);
            this.network = Network.StratisMain;

            for (int i = 0; i < 20; ++i)
            {
                BlockHeader header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
                header.Nonce = RandomUtils.GetUInt32();
                header.HashPrevBlock = chain.Tip.HashBlock;
                header.Bits = Target.Difficulty1;

                var chainedHeader = new ChainedHeader(header, header.GetHash(), chain.Tip);

                chain.SetTip(chainedHeader);

                this.chainedHeaders.Add(chainedHeader);
            }

            // Let block #5 be the consensus tip.
            this.chainState = new Mock<IChainState>().SetupProperty(x => x.ConsensusTip, this.chainedHeaders[5]);
        }

        /// <summary>
        /// Tests that a new tip will be selected if the only provider of the best tip disconnects.
        /// </summary>
        [Fact]
        public async Task NewTipIsSelectedWhenBestTipProviderDisconnectsAsync()
        {
            var chain = new ConcurrentChain(this.network);
            var chainSelector = new BestChainSelector(chain, this.chainState.Object, new LoggerFactory());

            chain.SetTip(this.chainedHeaders[10]);

            ChainedHeader tipFromFirstPeer = this.chainedHeaders[15];
            chainSelector.TrySetAvailableTip(0, tipFromFirstPeer);
            Assert.Equal(chain.Tip, tipFromFirstPeer);

            ChainedHeader tipFromSecondPeer = this.chainedHeaders[18];
            chainSelector.TrySetAvailableTip(1, tipFromSecondPeer);
            Assert.Equal(chain.Tip, tipFromSecondPeer);

            //Disconnect second peer
            chainSelector.RemoveAvailableTip(1);

            await Task.Delay(100).ConfigureAwait(false);

            Assert.Equal(chain.Tip, tipFromFirstPeer);

            //Disconnect first peer
            chainSelector.RemoveAvailableTip(0);

            await Task.Delay(100).ConfigureAwait(false);

            Assert.Equal(chain.Tip, this.chainState.Object.ConsensusTip);
        }

        /// <summary>
        /// Tests that a new tip will be selected if the only provider of the best tip disconnects.
        /// </summary>
        [Fact]
        public async Task CantSwitchToTipBelowConsensusAsync()
        {
            var chain = new ConcurrentChain(this.network);
            var chainSelector = new BestChainSelector(chain, this.chainState.Object, new LoggerFactory());

            chain.SetTip(this.chainedHeaders[10]);

            chainSelector.TrySetAvailableTip(0, this.chainedHeaders[15]);
            chainSelector.TrySetAvailableTip(1, this.chainedHeaders[2]);
            chainSelector.TrySetAvailableTip(2, this.chainedHeaders[3]);
            chainSelector.TrySetAvailableTip(3, this.chainedHeaders[4]);

            await Task.Delay(100).ConfigureAwait(false);

            Assert.Equal(chain.Tip, this.chainedHeaders[15]);

            chainSelector.RemoveAvailableTip(0);

            await Task.Delay(100).ConfigureAwait(false);

            Assert.Equal(chain.Tip, this.chainState.Object.ConsensusTip);
        }

        /// <summary>
        /// Tests that nothing happens if one of the best tip providers disconnects.
        /// </summary>
        [Fact]
        public async Task OneOfManyBestChainProvidersDisconnectsAsync()
        {
            var chain = new ConcurrentChain(this.network);
            var chainSelector = new BestChainSelector(chain, this.chainState.Object, new LoggerFactory());

            chain.SetTip(this.chainedHeaders[10]);

            ChainedHeader tip = this.chainedHeaders[15];

            chainSelector.TrySetAvailableTip(0, tip);
            chainSelector.TrySetAvailableTip(1, tip);
            chainSelector.TrySetAvailableTip(2, tip);

            Assert.Equal(chain.Tip, tip);

            chainSelector.RemoveAvailableTip(0);

            await Task.Delay(100).ConfigureAwait(false);

            Assert.Equal(chain.Tip, tip);
        }
    }
}