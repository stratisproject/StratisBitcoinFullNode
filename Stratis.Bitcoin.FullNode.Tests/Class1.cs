using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.FullNode.Consensus;
using System;
using System.Collections.Generic;
using System.Globalization;
using NBitcoin;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static NBitcoin.Transaction;
using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;

namespace Stratis.Bitcoin.FullNode.Tests
{
    public class Class1
    {
        [Fact]
        public void Valid()
        {
            BlockingCollection<Block> blocks = new BlockingCollection<Block>(new ConcurrentQueue<Block>(), 50);
            BlockStore store = new BlockStore("G:\\Bitcoin\\blocks", Network.Main);
            ChainedBlock tip = new ChainedBlock(store.Network.GetGenesis().Header, 0);
            new Thread(() =>
            {
                foreach(var b in Order(tip.HashBlock, store.Enumerate(DiskBlockPosRange.All).Select(b => b.Item)))
                {
                    blocks.Add(b);
                }
            }).Start();

            var validator = new ConsensusValidator(store.Network.Consensus);
            ThresholdConditionCache bip9 = new ThresholdConditionCache(store.Network.Consensus);
            bool blockOnFullQueue = false;
            foreach(var block in blocks.GetConsumingEnumerable())
            {
                try
                {
                    validator.CheckBlockHeader(block.Header);
                    var next = new ChainedBlock(block.Header, block.Header.GetHash(), tip);
                    var context = new ContextInformation(next, store.Network.Consensus);
                    validator.ContextualCheckBlockHeader(block.Header, context);
                    var states = bip9.GetStates(tip);
                    var flags = new ConsensusFlags(next, states, store.Network.Consensus);
                    validator.ContextualCheckBlock(block, flags, context);
                    validator.CheckBlock(block);

                    CoinViewBase coinView = new CoinViewBase();
                    validator.ExecuteBlock(block, next, flags, coinView, null);
                    
                    tip = next;
                    if(blockOnFullQueue && blocks.Count == blocks.BoundedCapacity)
                    {
                        Debugger.Break();
                    }
                }
                catch(ConsensusErrorException ex)
                {
                    Debugger.Break();
                }
            }
        }

        private IEnumerable<Block> Order(uint256 hashStart, IEnumerable<Block> blocks)
        {
            ConcurrentChain chain = new ConcurrentChain(Network.Main);
            chain.Load(File.ReadAllBytes("C:\\Bitcoin\\main.data"));
            var tip = chain.GetBlock(hashStart);


            Dictionary<uint256, Block> unprocessed = new Dictionary<uint256, Block>();
            foreach(var block in blocks)
            {
                var h = block.GetHash();
                if(!chain.Contains(h))
                    continue;
                var processed = Process(chain, ref tip, block, h);
                if(!processed)
                {
                    unprocessed.Add(h, block);
                }
                else
                    yield return block;

                foreach(var p in unprocessed.ToList())
                    if(Process(chain, ref tip, p.Value, p.Key))
                    {
                        unprocessed.Remove(p.Key);
                        yield return p.Value;
                    }
            }
        }

        private static bool Process(ConcurrentChain chain, ref ChainedBlock tip, Block block, uint256 h)
        {
            if(block.Header.HashPrevBlock == tip.HashBlock)
            {
                tip = chain.GetBlock(h);
                return true;
            }
            return false;
        }

        [Fact]
        public void CanCheckBlockWithWitness()
        {
            var block = new Block(Encoders.Hex.DecodeData("000000202f6f6a130549473222411b5c6f54150d63b32aadf10e57f7d563cfc7010000001e28204471ef9ef11acd73543894a96a3044932b85e99889e731322a8ec28a9f9ae9fc56ffff011d0011b40202010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff2c028027266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779010effffffff0250b5062a0100000023210263ed47e995cbbf1bc560101e3b76c6bdb1b094a185450cea533781ce598ff2b6ac0000000000000000266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779012000000000000000000000000000000000000000000000000000000000000000000000000001000000000101cecd90cd38ac6858c47f2fe9f28145d6e18f9c5abc7ef1a41e2f19e6fe0362580100000000ffffffff0130b48d06000000001976a91405481b7f1d90c5a167a15b00e8af76eb6984ea5988ac0247304402206104c335e4adbb920184957f9f710b09de17d015329fde6807b9d321fd2142db02200b24ad996b4aa4ff103000348b5ad690abfd9fddae546af9e568394ed4a83113012103a65786c1a48d4167aca08cf6eb8eed081e13f45c02dc6000fd8f3bb16242579a00000000"));

            var consensusFlags = new ConsensusFlags()
            {
                ScriptFlags = ScriptVerify.Witness | ScriptVerify.P2SH | ScriptVerify.Standard,
                LockTimeFlags = LockTimeFlags.MedianTimePast,
                EnforceBIP34 = true
            };

            var context = new ContextInformation()
            {
                BestBlock = new ContextBlockInformation()
                {
                    MedianTimePast = DateTimeOffset.Parse("2016-03-31T09:02:19+00:00", CultureInfo.InvariantCulture),
                    Height = 10111
                },
                NextWorkRequired = block.Header.Bits,
                Time = DateTimeOffset.UtcNow
            };
            var validator = new ConsensusValidator(new NBitcoin.Consensus());
            validator.CheckBlockHeader(block.Header);
            validator.ContextualCheckBlockHeader(block.Header, context);
            validator.ContextualCheckBlock(block, consensusFlags, context);
            validator.CheckBlock(block);
        }
    }
}
