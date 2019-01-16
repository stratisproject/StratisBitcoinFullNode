using System;
using System.Linq;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;
using static NBitcoin.Transaction;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class CoinViewTests
    {
        protected readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly Network regTest;
        private readonly DBreezeSerializer dBreezeSerializer;

        /// <summary>
        /// Initializes logger factory for tests in this class.
        /// </summary>
        public CoinViewTests()
        {
            this.loggerFactory = new LoggerFactory();
            this.network = KnownNetworks.Main;
            this.regTest = KnownNetworks.RegTest;
            this.dBreezeSerializer = new DBreezeSerializer(this.network);
        }

        [Fact]
        public void TestDBreezeSerialization()
        {
            using (NodeContext ctx = NodeContext.Create(this))
            {
                Block genesis = ctx.Network.GetGenesis();
                var genesisChainedHeader = new ChainedHeader(genesis.Header, ctx.Network.GenesisHash, 0);
                ChainedHeader chained = this.MakeNext(genesisChainedHeader, ctx.Network);
                ctx.PersistentCoinView.SaveChangesAsync(new UnspentOutputs[] { new UnspentOutputs(genesis.Transactions[0].GetHash(), new Coins(genesis.Transactions[0], 0)) }, null, genesisChainedHeader.HashBlock, chained.HashBlock, chained.Height).Wait();
                Assert.NotNull(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
                Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);

                ChainedHeader previous = chained;
                chained = this.MakeNext(this.MakeNext(genesisChainedHeader, ctx.Network), ctx.Network);
                chained = this.MakeNext(this.MakeNext(genesisChainedHeader, ctx.Network), ctx.Network);
                ctx.PersistentCoinView.SaveChangesAsync(new UnspentOutputs[0], null, previous.HashBlock, chained.HashBlock, chained.Height).Wait();
                Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetTipHashAsync().GetAwaiter().GetResult());
                ctx.ReloadPersistentCoinView();
                Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetTipHashAsync().GetAwaiter().GetResult());
                Assert.NotNull(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
                Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);
            }
        }

        [Fact]
        public void TestCacheCoinView()
        {
            using (NodeContext ctx = NodeContext.Create(this))
            {
                Block genesis = ctx.Network.GetGenesis();
                var genesisChainedHeader = new ChainedHeader(genesis.Header, ctx.Network.GenesisHash, 0);
                ChainedHeader chained = this.MakeNext(genesisChainedHeader, ctx.Network);
                var dateTimeProvider = new DateTimeProvider();

                var cacheCoinView = new CachedCoinView(ctx.PersistentCoinView, dateTimeProvider, this.loggerFactory, new NodeStats(dateTimeProvider));

                cacheCoinView.SaveChangesAsync(new UnspentOutputs[] { new UnspentOutputs(genesis.Transactions[0].GetHash(), new Coins(genesis.Transactions[0], 0)) }, null, genesisChainedHeader.HashBlock, chained.HashBlock, chained.Height).Wait();
                Assert.NotNull(cacheCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
                Assert.Null(cacheCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);
                Assert.Equal(chained.HashBlock, cacheCoinView.GetTipHashAsync().Result);

                Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
                Assert.Equal(chained.Previous.HashBlock, ctx.PersistentCoinView.GetTipHashAsync().Result);
                cacheCoinView.FlushAsync().GetAwaiter().GetResult();
                Assert.NotNull(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
                Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetTipHashAsync().Result);
                //Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);

                //var previous = chained;
                //chained = MakeNext(MakeNext(genesisChainedBlock));
                //chained = MakeNext(MakeNext(genesisChainedBlock));
                //ctx.PersistentCoinView.SaveChangesAsync(new UnspentOutputs[0], previous.HashBlock, chained.HashBlock).Wait();
                //Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetTipHashAsync().GetAwaiter().GetResult());
                //ctx.ReloadPersistentCoinView();
                //Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetTipHashAsync().GetAwaiter().GetResult());
                //Assert.NotNull(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
                //Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);
            }
        }

        [Fact]
        public void CanRewind()
        {
            using (NodeContext nodeContext = NodeContext.Create(this))
            {
                var dateTimeProvider = new DateTimeProvider();
                var cacheCoinView = new CachedCoinView(nodeContext.PersistentCoinView, dateTimeProvider, this.loggerFactory, new NodeStats(dateTimeProvider));
                var tester = new CoinViewTester(cacheCoinView);

                Coin[] coinsA = tester.CreateCoins(5);
                Coin[] coinsB = tester.CreateCoins(1);
                tester.NewBlock();
                cacheCoinView.FlushAsync().Wait();
                Assert.True(tester.Exists(coinsA[2]));
                Assert.True(tester.Exists(coinsB[0]));

                // Spend some coins.
                tester.Spend(coinsA[2]);
                tester.Spend(coinsB[0]);

                tester.NewBlock();

                // This will save an empty RewindData instance
                tester.NewBlock();

                // Create a new coin set/
                Coin[] coinsC = tester.CreateCoins(1);
                tester.NewBlock();
                Assert.True(tester.Exists(coinsA[0]));
                Assert.True(tester.Exists(coinsC[0]));
                Assert.False(tester.Exists(coinsA[2]));
                Assert.False(tester.Exists(coinsB[0]));

                // We need to rewind 3 times as we are now rewinding one block at a time.
                tester.Rewind(); // coinsC[0] should not exist any more.
                tester.Rewind(); // coinsA[2] should be spendable again.
                tester.Rewind(); // coinsB[2] should be spendable again.
                Assert.False(tester.Exists(coinsC[0]));
                Assert.True(tester.Exists(coinsA[2]));
                Assert.True(tester.Exists(coinsB[0]));

                // Spend some coins and esnure they are not spendable.
                tester.Spend(coinsA[2]);
                tester.Spend(coinsB[0]);
                tester.NewBlock();
                cacheCoinView.FlushAsync().Wait();
                Assert.False(tester.Exists(coinsA[2]));
                Assert.False(tester.Exists(coinsB[0]));

                // Rewind so that coinsA[2] and coinsB[0] become spendable again.
                tester.Rewind();
                Assert.True(tester.Exists(coinsA[2]));
                Assert.True(tester.Exists(coinsB[0]));

                // Create 7 coins in a new coin set and spend the first coin.
                Coin[] coinsD = tester.CreateCoins(7);
                tester.Spend(coinsD[0]);
                // Create a coin in a new coin set and spend it.
                Coin[] coinsE = tester.CreateCoins(1);
                tester.Spend(coinsE[0]);
                tester.NewBlock();

                Assert.True(tester.Exists(coinsD[1]));
                Assert.False(tester.Exists(coinsD[0]));
                cacheCoinView.FlushAsync().Wait();

                // Creates another empty RewindData instance.
                tester.NewBlock();

                // Rewind one block.
                tester.Rewind();

                // coinsD[1] was never touched, so should remain unchanged.
                // coinsD[0] was spent but the block in which the changes happened was not yet rewound to, so it remains unchanged.
                // coinsE[0] was spent but the block in which the changes happened was not yet rewound to, so it remains unchanged.
                // coinsA[1] was not touched, so should remain unchanged.
                // coinsB[1] was not touched, so should remain unchanged.
                Assert.True(tester.Exists(coinsD[1]));
                Assert.False(tester.Exists(coinsD[0]));
                Assert.False(tester.Exists(coinsE[0]));
                Assert.True(tester.Exists(coinsA[2]));
                Assert.True(tester.Exists(coinsB[0]));

                // Rewind one block.
                tester.Rewind();

                // coinsD[0] should now not exist in CoinView anymore.
                // coinsE[0] should now not exist in CoinView anymore.
                Assert.False(tester.Exists(coinsD[0]));
                Assert.False(tester.Exists(coinsE[0]));
            }
        }

        [Fact]
        public void CanHandleReorgs()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNode = builder.CreateStratisPowNode(this.regTest).Start();
                CoreNode coreNode1 = builder.CreateBitcoinCoreNode().Start();
                CoreNode coreNode2 = builder.CreateBitcoinCoreNode().Start();

                //Core1 discovers 10 blocks, sends to stratis
                coreNode1.FindBlock(10).Last();
                TestHelper.ConnectAndSync(stratisNode, coreNode1);
                TestHelper.Disconnect(stratisNode, coreNode1);

                //Core2 discovers 20 blocks, sends to stratis
                coreNode2.FindBlock(20).Last();
                TestHelper.ConnectAndSync(stratisNode, coreNode2);
                TestHelper.Disconnect(stratisNode, coreNode2);
                ((CachedCoinView)stratisNode.FullNode.CoinView()).FlushAsync().Wait();

                //Core1 discovers 30 blocks, sends to stratis
                coreNode1.FindBlock(30).Last();
                TestHelper.ConnectAndSync(stratisNode, coreNode1);
                TestHelper.Disconnect(stratisNode, coreNode1);

                //Core2 discovers 50 blocks, sends to stratis
                coreNode2.FindBlock(50).Last();
                TestHelper.ConnectAndSync(stratisNode, coreNode2);
                TestHelper.Disconnect(stratisNode, coreNode2);
                ((CachedCoinView)stratisNode.FullNode.CoinView()).FlushAsync().Wait();

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode, coreNode2));
            }
        }

        [Fact]
        public void TestDBreezeInsertOrder()
        {
            using (NodeContext ctx = NodeContext.Create(this))
            {
                using (var engine = new DBreeze.DBreezeEngine(ctx.FolderName + "/2"))
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

                    using (DBreeze.Transactions.Transaction tx = engine.GetTransaction())
                    {
                        foreach (uint256 d in data)
                            tx.Insert("Table", d.ToBytes(false), d.ToBytes());
                        tx.Commit();
                    }

                    var data2 = new uint256[data.Length];
                    using (DBreeze.Transactions.Transaction tx = engine.GetTransaction())
                    {
                        int i = 0;
                        foreach (Row<byte[], byte[]> row in tx.SelectForward<byte[], byte[]>("Table"))
                        {
                            data2[i++] = new uint256(row.Key, false);
                        }
                    }

                    Assert.True(data.SequenceEqual(data2));
                }
            }
        }

        private ChainedHeader MakeNext(ChainedHeader previous, Network network)
        {
            BlockHeader header = BlockHeader.Load(previous.Header.ToBytes(network.Consensus.ConsensusFactory), network);
            header.HashPrevBlock = previous.HashBlock;
            return new ChainedHeader(header, header.GetHash(), previous);
        }

        [Fact]
        public void CanSaveChainIncrementally()
        {
            using (var repo = new ChainRepository(TestBase.CreateTestDir(this), this.loggerFactory, this.dBreezeSerializer))
            {
                var chain = new ConcurrentChain(this.regTest);

                chain.SetTip(repo.LoadAsync(chain.Genesis).GetAwaiter().GetResult());
                Assert.True(chain.Tip == chain.Genesis);
                chain = new ConcurrentChain(this.regTest);
                ChainedHeader tip = this.AppendBlock(chain);
                repo.SaveAsync(chain).GetAwaiter().GetResult();
                var newChain = new ConcurrentChain(this.regTest);
                newChain.SetTip(repo.LoadAsync(chain.Genesis).GetAwaiter().GetResult());
                Assert.Equal(tip, newChain.Tip);
                tip = this.AppendBlock(chain);
                repo.SaveAsync(chain).GetAwaiter().GetResult();
                newChain = new ConcurrentChain(this.regTest);
                newChain.SetTip(repo.LoadAsync(chain.Genesis).GetAwaiter().GetResult());
                Assert.Equal(tip, newChain.Tip);
            }
        }

        public ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                Block block = this.network.CreateBlock();
                block.AddTransaction(this.network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedHeader AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chains);
        }

        [Fact]
        public void CanCheckBlockWithWitness()
        {
            Block block = Block.Load(Encoders.Hex.DecodeData("000000202f6f6a130549473222411b5c6f54150d63b32aadf10e57f7d563cfc7010000001e28204471ef9ef11acd73543894a96a3044932b85e99889e731322a8ec28a9f9ae9fc56ffff011d0011b40202010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff2c028027266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779010effffffff0250b5062a0100000023210263ed47e995cbbf1bc560101e3b76c6bdb1b094a185450cea533781ce598ff2b6ac0000000000000000266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779012000000000000000000000000000000000000000000000000000000000000000000000000001000000000101cecd90cd38ac6858c47f2fe9f28145d6e18f9c5abc7ef1a41e2f19e6fe0362580100000000ffffffff0130b48d06000000001976a91405481b7f1d90c5a167a15b00e8af76eb6984ea5988ac0247304402206104c335e4adbb920184957f9f710b09de17d015329fde6807b9d321fd2142db02200b24ad996b4aa4ff103000348b5ad690abfd9fddae546af9e568394ed4a83113012103a65786c1a48d4167aca08cf6eb8eed081e13f45c02dc6000fd8f3bb16242579a00000000"), this.network);

            var consensusFlags = new DeploymentFlags
            {
                ScriptFlags = ScriptVerify.Witness | ScriptVerify.P2SH | ScriptVerify.Standard,
                LockTimeFlags = LockTimeFlags.MedianTimePast,
                EnforceBIP34 = true
            };

            var context = new RuleContext
            {
                Time = DateTimeOffset.UtcNow,
                ValidationContext = new ValidationContext { BlockToValidate = block },
                Flags = consensusFlags,
            };

            this.network.Consensus.Options = new ConsensusOptions();
            new WitnessCommitmentsRule().RunAsync(context).GetAwaiter().GetResult();

            var rule = new CheckPowTransactionRule();
            var options = this.network.Consensus.Options;
            foreach (Transaction tx in block.Transactions)
                rule.CheckTransaction(this.network, options, tx);
        }
    }
}
