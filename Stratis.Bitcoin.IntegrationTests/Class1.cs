using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Consensus;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static NBitcoin.Transaction;
using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockPulling;
using System.Net.Http;
using System.Collections;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Utilities;
using NBitcoin.RPC;
using System.Net;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.IntegrationTests
{
	public class Class1
	{
		[Fact]
		public void TestDBreezeSerialization()
		{
			using(NodeContext ctx = NodeContext.Create())
			{
				var genesis = ctx.Network.GetGenesis();
				var genesisChainedBlock = new ChainedBlock(genesis.Header, 0);
				var chained = MakeNext(genesisChainedBlock);
				ctx.PersistentCoinView.SaveChangesAsync(new UnspentOutputs[] { new UnspentOutputs(genesis.Transactions[0].GetHash(), new Coins(genesis.Transactions[0], 0)) }, null, genesisChainedBlock.HashBlock, chained.HashBlock).Wait();
				Assert.NotNull(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
				Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);

				var previous = chained;
				chained = MakeNext(MakeNext(genesisChainedBlock));
				chained = MakeNext(MakeNext(genesisChainedBlock));
				ctx.PersistentCoinView.SaveChangesAsync(new UnspentOutputs[0], null, previous.HashBlock, chained.HashBlock).Wait();
				Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetBlockHashAsync().GetAwaiter().GetResult());
				ctx.ReloadPersistentCoinView();
				Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetBlockHashAsync().GetAwaiter().GetResult());
				Assert.NotNull(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
				Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);
			}
		}

		[Fact]
		public void TestCacheCoinView()
		{
			using(NodeContext ctx = NodeContext.Create())
			{
				var genesis = ctx.Network.GetGenesis();
				var genesisChainedBlock = new ChainedBlock(genesis.Header, 0);
				var chained = MakeNext(genesisChainedBlock);
				var cacheCoinView = new CachedCoinView(ctx.PersistentCoinView);

				cacheCoinView.SaveChangesAsync(new UnspentOutputs[] { new UnspentOutputs(genesis.Transactions[0].GetHash(), new Coins(genesis.Transactions[0], 0)) }, null, genesisChainedBlock.HashBlock, chained.HashBlock).Wait();
				Assert.NotNull(cacheCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
				Assert.Null(cacheCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);
				Assert.Equal(chained.HashBlock, cacheCoinView.GetBlockHashAsync().Result);

				Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
				Assert.Equal(chained.Previous.HashBlock, ctx.PersistentCoinView.GetBlockHashAsync().Result);
				cacheCoinView.FlushAsync().GetAwaiter().GetResult();
				Assert.NotNull(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
				Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetBlockHashAsync().Result);
				//Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);


				//var previous = chained;
				//chained = MakeNext(MakeNext(genesisChainedBlock));
				//chained = MakeNext(MakeNext(genesisChainedBlock));
				//ctx.PersistentCoinView.SaveChangesAsync(new UnspentOutputs[0], previous.HashBlock, chained.HashBlock).Wait();
				//Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetBlockHashAsync().GetAwaiter().GetResult());
				//ctx.ReloadPersistentCoinView();
				//Assert.Equal(chained.HashBlock, ctx.PersistentCoinView.GetBlockHashAsync().GetAwaiter().GetResult());
				//Assert.NotNull(ctx.PersistentCoinView.FetchCoinsAsync(new[] { genesis.Transactions[0].GetHash() }).Result.UnspentOutputs[0]);
				//Assert.Null(ctx.PersistentCoinView.FetchCoinsAsync(new[] { new uint256() }).Result.UnspentOutputs[0]);
			}
		}

		[Fact]
		public void CanRewind()
		{
			using(NodeContext ctx = NodeContext.Create())
			{
				var cacheCoinView = new CachedCoinView(ctx.PersistentCoinView);
				var tester = new CoinViewTester(cacheCoinView);

				var coins = tester.CreateCoins(5);
				var coin = tester.CreateCoins(1);

				// 1
				var h1 = tester.NewBlock();
				cacheCoinView.FlushAsync().Wait();
				Assert.True(tester.Exists(coins[2]));
				Assert.True(tester.Exists(coin[0]));

				tester.Spend(coins[2]);
				tester.Spend(coin[0]);
				//2
				tester.NewBlock();
				//3
				tester.NewBlock();
				//4
				var coin2 = tester.CreateCoins(1);
				tester.NewBlock();
				Assert.True(tester.Exists(coins[0]));
				Assert.True(tester.Exists(coin2[0]));
				Assert.False(tester.Exists(coins[2]));
				Assert.False(tester.Exists(coin[0]));
				//1
				tester.Rewind();
				Assert.False(tester.Exists(coin2[0]));
				Assert.True(tester.Exists(coins[2]));
				Assert.True(tester.Exists(coin[0]));


				tester.Spend(coins[2]);
				tester.Spend(coin[0]);
				//2
				var h2 = tester.NewBlock();
				cacheCoinView.FlushAsync().Wait();
				Assert.False(tester.Exists(coins[2]));
				Assert.False(tester.Exists(coin[0]));

				//1
				Assert.True(h1 == tester.Rewind());
				Assert.True(tester.Exists(coins[2]));
				Assert.True(tester.Exists(coin[0]));


				var coins2 = tester.CreateCoins(7);
				tester.Spend(coins2[0]);
				coin2 = tester.CreateCoins(1);
				tester.Spend(coin2[0]);
				//2
				tester.NewBlock();
				Assert.True(tester.Exists(coins2[1]));
				Assert.False(tester.Exists(coins2[0]));
				cacheCoinView.FlushAsync().Wait();
				//3
				tester.NewBlock();
				//2
				tester.Rewind();
				Assert.True(tester.Exists(coins2[1]));
				Assert.False(tester.Exists(coins2[0]));
				Assert.False(tester.Exists(coin2[0]));
				Assert.True(tester.Exists(coins[2]));
				Assert.True(tester.Exists(coin[0]));


			}
		}




		[Fact]
		public void NodesCanConnectToEachOthers()
		{
			using(NodeBuilder builder = NodeBuilder.Create())
			{
				var node1 = builder.CreateStratisNode();
				var node2 = builder.CreateStratisNode();
				builder.StartAll();
				Assert.Equal(0, node1.FullNode.ConnectionManager.ConnectedNodes.Count);
				Assert.Equal(0, node2.FullNode.ConnectionManager.ConnectedNodes.Count);
				var rpc1 = node1.CreateRPCClient();
				var rpc2 = node2.CreateRPCClient();
				rpc1.AddNode(node2.Endpoint, true);
				Assert.Equal(1, node1.FullNode.ConnectionManager.ConnectedNodes.Count);
				Assert.Equal(1, node2.FullNode.ConnectionManager.ConnectedNodes.Count);

				var behavior = node1.FullNode.ConnectionManager.ConnectedNodes.First().Behaviors.Find<ConnectionManagerBehavior>();
				Assert.False(behavior.Inbound);
				Assert.True(behavior.OneTry);
				behavior = node2.FullNode.ConnectionManager.ConnectedNodes.First().Behaviors.Find<ConnectionManagerBehavior>();
				Assert.True(behavior.Inbound);
				Assert.False(behavior.OneTry);
			}
		}


		[Fact]
		public void CanHandleReorgs()
		{
			using(NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNode = builder.CreateStratisNode();
				var coreNode1 = builder.CreateNode();
				var coreNode2 = builder.CreateNode();
				builder.StartAll();

				//Core1 discovers 10 blocks, sends to stratis
				var tip = coreNode1.FindBlock(10).Last();
				stratisNode.CreateRPCClient().AddNode(coreNode1.Endpoint, true);
				Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode1.CreateRPCClient().GetBestBlockHash());
				stratisNode.CreateRPCClient().RemoveNode(coreNode1.Endpoint);

				//Core2 discovers 20 blocks, sends to stratis
				tip = coreNode2.FindBlock(20).Last();
				stratisNode.CreateRPCClient().AddNode(coreNode2.Endpoint, true);
				Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode2.CreateRPCClient().GetBestBlockHash());
				stratisNode.CreateRPCClient().RemoveNode(coreNode2.Endpoint);
				((CachedCoinView)stratisNode.FullNode.CoinView).FlushAsync().Wait();

				//Core1 discovers 30 blocks, sends to stratis
				tip = coreNode1.FindBlock(30).Last();
				stratisNode.CreateRPCClient().AddNode(coreNode1.Endpoint, true);
				Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode1.CreateRPCClient().GetBestBlockHash());
				stratisNode.CreateRPCClient().RemoveNode(coreNode1.Endpoint);

				//Core2 discovers 50 blocks, sends to stratis
				tip = coreNode2.FindBlock(50).Last();
				stratisNode.CreateRPCClient().AddNode(coreNode2.Endpoint, true);
				Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode2.CreateRPCClient().GetBestBlockHash());
				stratisNode.CreateRPCClient().RemoveNode(coreNode2.Endpoint);
				((CachedCoinView)stratisNode.FullNode.CoinView).FlushAsync().Wait();
			}
		}

		[Fact]
		public void CanStratisSyncFromCore()
		{
			using(NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNode = builder.CreateStratisNode();
				var coreNode = builder.CreateNode();
				builder.StartAll();
				
				// not in IBD
				stratisNode.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

				var tip = coreNode.FindBlock(10).Last();
				stratisNode.CreateRPCClient().AddNode(coreNode.Endpoint, true);
				Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());
				var bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
				Assert.Equal(tip.GetHash(), bestBlockHash);

				//Now check if Core connect to stratis
				stratisNode.CreateRPCClient().RemoveNode(coreNode.Endpoint);
				tip = coreNode.FindBlock(10).Last();
				coreNode.CreateRPCClient().AddNode(stratisNode.Endpoint, true);
				Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());
				bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
				Assert.Equal(tip.GetHash(), bestBlockHash);


				//For Core synching from Stratis, need to save blocks in stratis
			}
		}

		[Fact]
		public void CanStratisSyncFromStratis()
		{
			using (NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNode = builder.CreateStratisNode();
				var stratisNodeSync = builder.CreateStratisNode();
				var coreCreateNode = builder.CreateNode();
				builder.StartAll();

				// not in IBD
				stratisNode.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));
				stratisNodeSync.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

				// first seed a core node with blocks and sync them to a stratis node
				// and wait till the stratis node is fully synced
				var tip = coreCreateNode.FindBlock(5).Last();
				stratisNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
				Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
				var bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
				Assert.Equal(tip.GetHash(), bestBlockHash);

				// add a new stratis node which will download
				// the blocks using the GetData payload
				stratisNodeSync.CreateRPCClient().AddNode(stratisNode.Endpoint, true);

				// wait for download and assert
				Class1.Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
				bestBlockHash = stratisNodeSync.CreateRPCClient().GetBestBlockHash();
				Assert.Equal(tip.GetHash(), bestBlockHash);

			}
		}

		[Fact]
		public void CanCoreSyncFromStratis()
		{
			using (NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNode = builder.CreateStratisNode();
				var coreNodeSync = builder.CreateNode();
				var coreCreateNode = builder.CreateNode();
				builder.StartAll();

				// not in IBD
				stratisNode.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

				// first seed a core node with blocks and sync them to a stratis node
				// and wait till the stratis node is fully synced
				var tip = coreCreateNode.FindBlock(5).Last();
				stratisNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
				Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
				Class1.Eventually(() => stratisNode.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNode.FullNode.Chain.Tip.HashBlock);

				var bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
				Assert.Equal(tip.GetHash(), bestBlockHash);

				// add a new stratis node which will download
				// the blocks using the GetData payload
				coreNodeSync.CreateRPCClient().AddNode(stratisNode.Endpoint, true);

				// wait for download and assert
				Class1.Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNodeSync.CreateRPCClient().GetBestBlockHash());
				bestBlockHash = coreNodeSync.CreateRPCClient().GetBestBlockHash();
				Assert.Equal(tip.GetHash(), bestBlockHash);
			}
		}

		public static void Eventually(Func<bool> act)
		{
			var cancel = new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 30 * 1000);
			while(!act())
			{
				cancel.Token.ThrowIfCancellationRequested();
				Thread.Sleep(50);
			}
		}

		[Fact]
		public void CheckRPCFailures()
		{
			using(NodeBuilder builder = NodeBuilder.Create())
			{
				var node = builder.CreateStratisNode();
				builder.StartAll();
				var client = node.CreateRPCClient();
				var hash = client.GetBestBlockHash();
				try
				{
					client.SendCommand("lol");
					Assert.True(false, "should throw");
				}
				catch(RPCException ex)
				{
					Assert.Equal(RPCErrorCode.RPC_METHOD_NOT_FOUND, ex.RPCCode);
				}
				Assert.Equal(hash, Network.RegTest.GetGenesis().GetHash());
				var oldClient = client;
				client = new NBitcoin.RPC.RPCClient("abc:def", client.Address, client.Network);
				try
				{
					client.GetBestBlockHash();
					Assert.True(false, "should throw");
				}
				catch(Exception ex)
				{
					Assert.True(ex.Message.Contains("401"));
				}
				client = oldClient;

				try
				{
					client.SendCommand("addnode", "regreg", "addr");
					Assert.True(false, "should throw");
				}
				catch(RPCException ex)
				{
					Assert.Equal(RPCErrorCode.RPC_MISC_ERROR, ex.RPCCode);
				}

			}
		}

		[Fact]
		public void TestDBreezeInsertOrder()
		{
			using(NodeContext ctx = NodeContext.Create())
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
		public void CanSaveChainIncrementally()
		{
			using(var dir = TestDirectory.Create())
			{
				using(var repo = new ChainRepository(dir.FolderName))
				{
					var chain = new ConcurrentChain(Network.RegTest);
					repo.Load(chain).GetAwaiter().GetResult();
					Assert.True(chain.Tip == chain.Genesis);
					chain = new ConcurrentChain(Network.RegTest);
					var tip = AppendBlock(chain);
					repo.Save(chain).GetAwaiter().GetResult();
					var newChain = new ConcurrentChain(Network.RegTest);
					repo.Load(newChain).GetAwaiter().GetResult();
					Assert.Equal(tip, newChain.Tip);
					tip = AppendBlock(chain);
					repo.Save(chain).GetAwaiter().GetResult();
					newChain = new ConcurrentChain(Network.RegTest);
					repo.Load(newChain).GetAwaiter().GetResult();
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

		private byte[] GetFile(string fileName, string url)
		{
			fileName = Path.Combine("TestData", fileName);
			if(File.Exists(fileName))
				return File.ReadAllBytes(fileName);
			HttpClient client = new HttpClient();
			client.Timeout = TimeSpan.FromMinutes(10);
			var data = client.GetByteArrayAsync(url).Result;
			File.WriteAllBytes(fileName, data);
			return data;
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
