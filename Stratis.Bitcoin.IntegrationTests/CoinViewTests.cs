using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using static NBitcoin.Transaction;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class CoinViewTests
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// Initializes logger factory for tests in this class.
        /// </summary>
        public CoinViewTests()
        {
            this.loggerFactory = new LoggerFactory();
        }

        [Fact]
        public async Task TestDBreezeSerializationAsync()
        {
            using(NodeContext ctx = await NodeContext.CreateAsync().ConfigureAwait(false))
            {
                var genesis = ctx.Network.GetGenesis();
                var genesisChainedBlock = new ChainedBlock(genesis.Header, 0);
                var chained = MakeNext(genesisChainedBlock);
                await ctx.PersistentCoinView.SaveChangesAsync(new UnspentOutputs[] { new UnspentOutputs(genesis.Transactions[0].GetHash(), new Coins(genesis.Transactions[0], 0)) }, null, genesisChainedBlock.HashBlock, chained.HashBlock).ConfigureAwait(false);
                Assert.NotNull((await ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).ConfigureAwait(false)).UnspentOutputs[0]);
                Assert.Null((await ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).ConfigureAwait(false)).UnspentOutputs[0]);

                var previous = chained;
                chained = MakeNext(MakeNext(genesisChainedBlock));
                chained = MakeNext(MakeNext(genesisChainedBlock));
                await ctx.PersistentCoinView.SaveChangesAsync(new UnspentOutputs[0], null, previous.HashBlock, chained.HashBlock).ConfigureAwait(false);
                Assert.Equal(chained.HashBlock, await ctx.PersistentCoinView.GetBlockHashAsync().ConfigureAwait(false));
                await ctx.ReloadPersistentCoinViewAsync().ConfigureAwait(false);
                Assert.Equal(chained.HashBlock, await ctx.PersistentCoinView.GetBlockHashAsync().ConfigureAwait(false));
                Assert.NotNull((await ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).ConfigureAwait(false)).UnspentOutputs[0]);
                Assert.Null((await ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).ConfigureAwait(false)).UnspentOutputs[0]);
            }
        }

        [Fact]
        public async Task TestCacheCoinViewAsync()
        {
            using(NodeContext ctx = await NodeContext.CreateAsync().ConfigureAwait(false))
            {
                var genesis = ctx.Network.GetGenesis();
                var genesisChainedBlock = new ChainedBlock(genesis.Header, 0);
                var chained = MakeNext(genesisChainedBlock);
                var cacheCoinView = new CachedCoinView(ctx.PersistentCoinView, this.loggerFactory);

                await cacheCoinView.SaveChangesAsync(new UnspentOutputs[] { new UnspentOutputs(genesis.Transactions[0].GetHash(), new Coins(genesis.Transactions[0], 0)) }, null, genesisChainedBlock.HashBlock, chained.HashBlock).ConfigureAwait(false);
                Assert.NotNull((await cacheCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).ConfigureAwait(false)).UnspentOutputs[0]);
                Assert.Null((await cacheCoinView.FetchCoinsAsync(new[] { new uint256() }).ConfigureAwait(false)).UnspentOutputs[0]);
                Assert.Equal(chained.HashBlock, (await cacheCoinView.GetBlockHashAsync().ConfigureAwait(false)));

                Assert.Null((await ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).ConfigureAwait(false)).UnspentOutputs[0]);
                Assert.Equal(chained.Previous.HashBlock, await ctx.PersistentCoinView.GetBlockHashAsync().ConfigureAwait(false));
                await cacheCoinView.FlushAsync().ConfigureAwait(false);
                Assert.NotNull((await ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).ConfigureAwait(false)).UnspentOutputs[0]);
                Assert.Equal(chained.HashBlock, await ctx.PersistentCoinView.GetBlockHashAsync().ConfigureAwait(false));
                //Assert.Null((await ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).ConfigureAwait(false)).UnspentOutputs[0]);


                //var previous = chained;
                //chained = MakeNext(MakeNext(genesisChainedBlock));
                //chained = MakeNext(MakeNext(genesisChainedBlock));
                //await ctx.PersistentCoinView.SaveChangesAsync(new UnspentOutputs[0], previous.HashBlock, chained.HashBlock).ConfigureAwait(false);
                //Assert.Equal(chained.HashBlock, await ctx.PersistentCoinView.GetBlockHashAsync().ConfigureAwait(false));
                //await ctx.ReloadPersistentCoinViewAsync().ConfigureAwait(false)
                //Assert.Equal(chained.HashBlock, await ctx.PersistentCoinView.GetBlockHashAsync().ConfigureAwait(false));
                //Assert.NotNull((await ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).ConfigureAwait(false)).UnspentOutputs[0]);
                //Assert.Null((await ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).ConfigureAwait(false)).UnspentOutputs[0]);
            }
        }

        [Fact]
        public async Task CanRewindAsync()
        {
            using(NodeContext ctx = await NodeContext.CreateAsync().ConfigureAwait(false))
            {
                var cacheCoinView = new CachedCoinView(ctx.PersistentCoinView, this.loggerFactory);
                var tester = new CoinViewTester(cacheCoinView);
                await tester.InitializeAsync().ConfigureAwait(false);

                var coins = tester.CreateCoins(5);
                var coin = tester.CreateCoins(1);

                // 1
                var h1 = await tester.NewBlockAsync().ConfigureAwait(false);
                await cacheCoinView.FlushAsync().ConfigureAwait(false);
                Assert.True(await tester.ExistsAsync(coins[2]).ConfigureAwait(false));
                Assert.True(await tester.ExistsAsync(coin[0]).ConfigureAwait(false));

                await tester.SpendAsync(coins[2]).ConfigureAwait(false);
                await tester.SpendAsync(coin[0]).ConfigureAwait(false);
                //2
                await tester.NewBlockAsync().ConfigureAwait(false);
                //3
                await tester.NewBlockAsync().ConfigureAwait(false);
                //4
                var coin2 = tester.CreateCoins(1);
                await tester.NewBlockAsync().ConfigureAwait(false);
                Assert.True(await tester.ExistsAsync(coins[0]).ConfigureAwait(false));
                Assert.True(await tester.ExistsAsync(coin2[0]).ConfigureAwait(false));
                Assert.False(await tester.ExistsAsync(coins[2]).ConfigureAwait(false));
                Assert.False(await tester.ExistsAsync(coin[0]).ConfigureAwait(false));
                //1
                await tester.RewindAsync().ConfigureAwait(false);
                Assert.False(await tester.ExistsAsync(coin2[0]).ConfigureAwait(false));
                Assert.True(await tester.ExistsAsync(coins[2]).ConfigureAwait(false));
                Assert.True(await tester.ExistsAsync(coin[0]).ConfigureAwait(false));


                await tester.SpendAsync(coins[2]).ConfigureAwait(false);
                await tester.SpendAsync(coin[0]).ConfigureAwait(false);
                //2
                var h2 = await tester.NewBlockAsync().ConfigureAwait(false);
                await cacheCoinView.FlushAsync().ConfigureAwait(false);
                Assert.False(await tester.ExistsAsync(coins[2]).ConfigureAwait(false));
                Assert.False(await tester.ExistsAsync(coin[0]).ConfigureAwait(false));

                //1
                Assert.True(h1 == await tester.RewindAsync().ConfigureAwait(false));
                Assert.True(await tester.ExistsAsync(coins[2]).ConfigureAwait(false));
                Assert.True(await tester.ExistsAsync(coin[0]).ConfigureAwait(false));


                var coins2 = tester.CreateCoins(7);
                await tester.SpendAsync(coins2[0]).ConfigureAwait(false);
                coin2 = tester.CreateCoins(1);
                await tester.SpendAsync(coin2[0]).ConfigureAwait(false);
                //2
                await tester.NewBlockAsync().ConfigureAwait(false);
                Assert.True(await tester.ExistsAsync(coins2[1]).ConfigureAwait(false));
                Assert.False(await tester.ExistsAsync(coins2[0]).ConfigureAwait(false));
                await cacheCoinView.FlushAsync().ConfigureAwait(false);
                //3
                await tester.NewBlockAsync().ConfigureAwait(false);
                //2
                await tester.RewindAsync().ConfigureAwait(false);
                Assert.True(await tester.ExistsAsync(coins2[1]).ConfigureAwait(false));
                Assert.False(await tester.ExistsAsync(coins2[0]).ConfigureAwait(false));
                Assert.False(await tester.ExistsAsync(coin2[0]).ConfigureAwait(false));
                Assert.True(await tester.ExistsAsync(coins[2]).ConfigureAwait(false));
                Assert.True(await tester.ExistsAsync(coin[0]).ConfigureAwait(false));


            }
        }

        [Fact]
        public async Task CanHandleReorgsAsync()
        {
            using(NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var stratisNode = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var coreNode1 = await builder.CreateNodeAsync().ConfigureAwait(false);
                var coreNode2 = await builder.CreateNodeAsync().ConfigureAwait(false);
                builder.StartAll();

                //Core1 discovers 10 blocks, sends to stratis
                var tip = (await coreNode1.FindBlockAsync(10).ConfigureAwait(false)).Last();
                stratisNode.CreateRPCClient().AddNode(coreNode1.Endpoint, true);
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode1.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                stratisNode.CreateRPCClient().RemoveNode(coreNode1.Endpoint);

                //Core2 discovers 20 blocks, sends to stratis
                tip = (await coreNode2.FindBlockAsync(20).ConfigureAwait(false)).Last();
                stratisNode.CreateRPCClient().AddNode(coreNode2.Endpoint, true);
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode2.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                stratisNode.CreateRPCClient().RemoveNode(coreNode2.Endpoint);
                await ((CachedCoinView)stratisNode.FullNode.CoinView()).FlushAsync().ConfigureAwait(false);

                //Core1 discovers 30 blocks, sends to stratis
                tip = (await coreNode1.FindBlockAsync(30).ConfigureAwait(false)).Last();
                stratisNode.CreateRPCClient().AddNode(coreNode1.Endpoint, true);
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode1.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                stratisNode.CreateRPCClient().RemoveNode(coreNode1.Endpoint);

                //Core2 discovers 50 blocks, sends to stratis
                tip = (await coreNode2.FindBlockAsync(50).ConfigureAwait(false)).Last();
                stratisNode.CreateRPCClient().AddNode(coreNode2.Endpoint, true);
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode2.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                stratisNode.CreateRPCClient().RemoveNode(coreNode2.Endpoint);
                await ((CachedCoinView)stratisNode.FullNode.CoinView()).FlushAsync().ConfigureAwait(false);

                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode2.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task TestDBreezeInsertOrderAsync()
        {
            using(NodeContext ctx = await NodeContext.CreateAsync().ConfigureAwait(false))
            {
                using(var engine = new DBreeze.DBreezeEngine(ctx.FolderName + "/2"))
                {
                    var data = new[]
                    {
                        new uint256(3),
                        new uint256(2),
                        new uint256(2439425),
                        new uint256(5),
                        new uint256(243945),
                        new uint256(10),
                        new uint256(Hashes.Hash256(new byte[0])),
                        new uint256(Hashes.Hash256(new byte[] {1})),
                        new uint256(Hashes.Hash256(new byte[] {2})),
                    };
                    Array.Sort(data, new UInt256Comparer());

                    using(var tx = engine.GetTransaction())
                    {
                        foreach(var d in data)
                            tx.Insert("Table", d.ToBytes(false), d.ToBytes());
                        tx.Commit();
                    }
                    var data2 = new uint256[data.Length];
                    using(var tx = engine.GetTransaction())
                    {
                        int i = 0;
                        foreach(var row in tx.SelectForward<byte[], byte[]>("Table"))
                        {
                            data2[i++] = new uint256(row.Key, false);
                        }
                    }
                    Assert.True(data.SequenceEqual(data2));
                }
            }
        }

        private ChainedBlock MakeNext(ChainedBlock previous)
        {
            var header = previous.Header.Clone();
            header.HashPrevBlock = previous.HashBlock;
            return new ChainedBlock(header, null, previous);
        }

        [Fact]
        public async Task CanSaveChainIncrementallyAsync()
        {
            using(var dir = TestDirectory.Create())
            {
                using(var repo = new ChainRepository(dir.FolderName))
                {
                    var chain = new ConcurrentChain(Network.RegTest);
                    await repo.Load(chain).ConfigureAwait(false);
                    Assert.True(chain.Tip == chain.Genesis);
                    chain = new ConcurrentChain(Network.RegTest);
                    var tip = AppendBlock(chain);
                    await repo.Save(chain).ConfigureAwait(false);
                    var newChain = new ConcurrentChain(Network.RegTest);
                    await repo.Load(newChain).ConfigureAwait(false);
                    Assert.Equal(tip, newChain.Tip);
                    tip = AppendBlock(chain);
                    await repo.Save(chain).ConfigureAwait(false);
                    newChain = new ConcurrentChain(Network.RegTest);
                    await repo.Load(newChain).ConfigureAwait(false);
                    Assert.Equal(tip, newChain.Tip);
                }
            }
        }

        public ChainedBlock AppendBlock(ChainedBlock previous, params ConcurrentChain[] chains)
        {
            ChainedBlock last = null;
            var nonce = RandomUtils.GetUInt32();
            foreach(var chain in chains)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if(!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedBlock AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedBlock index = null;
            return AppendBlock(index, chains);
        }

        private async Task<byte[]> GetFileAsync(string fileName, string url)
        {
            fileName = Path.Combine("TestData", fileName);
            if(File.Exists(fileName))
                return await File.ReadAllBytesAsync(fileName).ConfigureAwait(false);
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            var data = await client.GetByteArrayAsync(url).ConfigureAwait(false);
            await File.WriteAllBytesAsync(fileName, data).ConfigureAwait(false);
            return data;
        }

        [Fact]
        public void CanCheckBlockWithWitness()
        {
            var block = new Block(Encoders.Hex.DecodeData("000000202f6f6a130549473222411b5c6f54150d63b32aadf10e57f7d563cfc7010000001e28204471ef9ef11acd73543894a96a3044932b85e99889e731322a8ec28a9f9ae9fc56ffff011d0011b40202010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff2c028027266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779010effffffff0250b5062a0100000023210263ed47e995cbbf1bc560101e3b76c6bdb1b094a185450cea533781ce598ff2b6ac0000000000000000266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779012000000000000000000000000000000000000000000000000000000000000000000000000001000000000101cecd90cd38ac6858c47f2fe9f28145d6e18f9c5abc7ef1a41e2f19e6fe0362580100000000ffffffff0130b48d06000000001976a91405481b7f1d90c5a167a15b00e8af76eb6984ea5988ac0247304402206104c335e4adbb920184957f9f710b09de17d015329fde6807b9d321fd2142db02200b24ad996b4aa4ff103000348b5ad690abfd9fddae546af9e568394ed4a83113012103a65786c1a48d4167aca08cf6eb8eed081e13f45c02dc6000fd8f3bb16242579a00000000"));

            var consensusFlags = new DeploymentFlags()
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
                Time = DateTimeOffset.UtcNow,
                BlockResult = new BlockResult { Block = block },
                Flags = consensusFlags,
            };
            Network.Main.Consensus.Options = new PowConsensusOptions();
            var validator = new PowConsensusValidator(Network.Main, new Checkpoints(Network.Main), new LoggerFactory());
            //validator.CheckBlockHeader(context);
            validator.ContextualCheckBlock(context);
            validator.CheckBlock(context);
        }
    }
}
