using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBreeze;
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
        private readonly List<ChainedBlock> chainedBlocks;

        public BestChainSelectorTest()
        {
            this.chainedBlocks = new List<ChainedBlock>();
            var chain = new ConcurrentChain(Network.StratisMain);

            for (int i = 0; i < 20; ++i)
            {
                var header = new BlockHeader()
                {
                    Nonce = RandomUtils.GetUInt32(),
                    HashPrevBlock = chain.Tip.HashBlock,
                    Bits = Target.Difficulty1
                };

                var chainedBlock = new ChainedBlock(header, header.GetHash(), chain.Tip);

                chain.SetTip(chainedBlock);
                
                this.chainedBlocks.Add(chainedBlock);
            }

            // Let block #5 be the consensus tip.
            this.chainState = new Mock<IChainState>().SetupProperty(x => x.ConsensusTip, this.chainedBlocks[5]);
        }

        /// <summary>
        /// Tests that a new tip will be selected if the only provider of the best tip disconnects.
        /// </summary>
        [Fact]
        public async Task NewTipIsSelectedWhenBestTipProviderDisconnectsAsync()
        {
            var chain = new ConcurrentChain(Network.StratisMain);
            var chainSelector = new BestChainSelector(chain, this.chainState.Object, new LoggerFactory());

            chain.SetTip(this.chainedBlocks[10]);

            ChainedBlock tipFromFirstPeer = this.chainedBlocks[15];
            chainSelector.TrySetAvailableTip(0, tipFromFirstPeer);
            Assert.Equal(chain.Tip, tipFromFirstPeer);

            ChainedBlock tipFromSecondPeer = this.chainedBlocks[18];
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
            var chain = new ConcurrentChain(Network.StratisMain);
            var chainSelector = new BestChainSelector(chain, this.chainState.Object, new LoggerFactory());

            chain.SetTip(this.chainedBlocks[10]);
            
            chainSelector.TrySetAvailableTip(0, this.chainedBlocks[15]);
            chainSelector.TrySetAvailableTip(1, this.chainedBlocks[2]);
            chainSelector.TrySetAvailableTip(2, this.chainedBlocks[3]);
            chainSelector.TrySetAvailableTip(3, this.chainedBlocks[4]);
            
            await Task.Delay(100).ConfigureAwait(false);

            Assert.Equal(chain.Tip, this.chainedBlocks[15]);

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
            var chain = new ConcurrentChain(Network.StratisMain);
            var chainSelector = new BestChainSelector(chain, this.chainState.Object, new LoggerFactory());

            chain.SetTip(this.chainedBlocks[10]);

            ChainedBlock tip = this.chainedBlocks[15];

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
