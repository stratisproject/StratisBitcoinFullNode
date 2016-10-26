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
using NBitcoin.Protocol;
using Stratis.Bitcoin.FullNode.BlockPulling;
using System.Net.Http;

namespace Stratis.Bitcoin.FullNode.Tests
{
	public class Class1
	{
		[Fact]
		public void ValidSomeBlocks()
		{
			var network = NBitcoin.Network.Main;
			var chain = new ConcurrentChain(network.GetGenesis().Header);
			if(network == NBitcoin.Network.Main)
			{
				chain.Load(GetFile("main.data", "https://aois.blob.core.windows.net/public/main.data"));
			}
			else
			{
				chain.Load(GetFile("test.data", "https://aois.blob.core.windows.net/public/test.data"));
			}
			ConsensusValidator valid = new ConsensusValidator(network.Consensus);
			Node node = Node.Connect(network, "yournode");
			node.VersionHandshake();
			var coinview = new CoinViewBase();
			coinview.Tip = new ChainedBlock(network.GetGenesis().Header, 0);
			var puller = new CustomNodeBlockPuller(chain, node);
			var lastSnapshot = valid.PerformanceCounter.Snapshot();
			foreach(var block in valid.Run(coinview, puller))
			{
				if((DateTimeOffset.UtcNow - lastSnapshot.Taken) > TimeSpan.FromSeconds(5.0))
				{
					Console.WriteLine();
					Console.WriteLine("Height: " + puller.Location.Height);
					var snapshot = valid.PerformanceCounter.Snapshot();
					Console.WriteLine(snapshot - lastSnapshot);
					lastSnapshot = snapshot;
				}
			}
		}

		private byte[] GetFile(string fileName, string url)
		{
			if(File.Exists(fileName))
				return File.ReadAllBytes(fileName);
			HttpClient client = new HttpClient();
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
