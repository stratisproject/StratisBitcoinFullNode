using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Protocol;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class BlockStoreTests
    {
		[Fact]
		public void BlockRepositoryPutGetDeleteBlock()
	    {
			using (var dir = TestDirectory.Create())
			{
				using (var blockRepo = new BlockStore.BlockRepository(dir.FolderName))
				{
					var lst = new List<Block>();
					for (int i = 0; i < 5; i++)
					{
						// put
						var block = new Block();
						block.AddTransaction(new Transaction());
						block.AddTransaction(new Transaction());
						block.UpdateMerkleRoot();
						block.Header.HashPrevBlock = lst.Any() ? lst.Last().GetHash() : Network.Main.GenesisHash;
						blockRepo.PutAsync(block).GetAwaiter().GetResult();

						// get
						var received = blockRepo.GetAsync(block.GetHash()).GetAwaiter().GetResult();
						Assert.True(block.ToBytes().SequenceEqual(received.ToBytes()));

						lst.Add(block);
					}

					// check each block
					foreach (var block in lst)
					{
						var received = blockRepo.GetAsync(block.GetHash()).GetAwaiter().GetResult();
						Assert.True(block.ToBytes().SequenceEqual(received.ToBytes()));
					}

					// delete
					blockRepo.DeleteAsync(lst.ElementAt(2).GetHash());
					var deleted = blockRepo.GetAsync(lst.ElementAt(2).GetHash()).GetAwaiter().GetResult();
					Assert.Null(deleted);
				}
			}
		}

		[Fact]
		public void NodeRespondToGetData()
		{
		    using (NodeBuilder builder = NodeBuilder.Create())
		    {
				var stratisNode = builder.CreateStratisNode();
				var stratisNodeSync = builder.CreateStratisNode();
				var coreCreateNode = builder.CreateNode();
				builder.StartAll();

				// core discovers 10 blocks, sends to stratis 
				var tip = coreCreateNode.FindBlock(5).Last();
				stratisNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
				Class1.Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());

				// add a new stratis node, the node will download
				// this will call GetData payload
				stratisNodeSync.CreateRPCClient().AddNode(stratisNode.Endpoint, true);

				// wait for download and assert
				Class1.Eventually(() => stratisNode.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
				Assert.True(stratisNode.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
		    }
	    }
    }
}
