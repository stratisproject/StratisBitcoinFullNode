C# Coding Style
===============

For non code files (xml etc) our current best guidance is consistency. When editing files, keep new code and changes consistent with the style in the files. For new files, it should conform to the style for that component. Last, if there's a completely new component, anything that is reasonably broadly accepted is fine.

The general rules:

1. We use [Allman style](http://en.wikipedia.org/wiki/Indent_style#Allman_style) braces, where each brace begins on a new line. A single line statement block can go without braces, one line statments are allowed (without braces) if it makes readbility better. 
2. We use four spaces of indentation (no tabs).
3. We use `camelCase` for internal and private fields and use `readonly` where possible. When used static fields, `readonly` should come after `static` (i.e. `static readonly` not `readonly static`).
4. We always use `this.` to easily distinguish instance and methods arguments. 
5. We always specify the visibility, even if it's the default (i.e.
   `private string foo` not `string foo`). Visibility should be the first modifier (i.e. 
   `public abstract` not `abstract public`).
6. Namespace imports should be specified at the top of the file, *outside* of
   `namespace` declarations and should be sorted alphabetically.
7. Avoid more than one empty line at any time. For example, do not have two
   blank lines between members of a type.
8. Avoid spurious free spaces.
   For example avoid `if (someVar == 0)...`, where the dots mark the spurious free spaces.
   Consider enabling "View White Space (Ctrl+E, S)" if using Visual Studio, to aid detection.
9. If a file happens to differ in style from these guidelines (e.g. private members are named `_member`
   rather than `member`), change it to the guidline style.
10. We only use `var` when it's obvious what the variable type is (i.e. `var stream = new FileStream(...)` not `var stream = OpenStandardInput()`).
11. We use language keywords instead of BCL types (i.e. `int, string, float` instead of `Int32, String, Single`, etc) for both type references as well as method calls (i.e. `int.Parse` instead of `Int32.Parse`).
12. We use PascalCasing to name all our constant local variables and fields.
13. We use ```nameof(...)``` instead of ```"..."``` whenever possible and relevant.
14. Fields should be specified at the top within type declarations.
15. When including non-ASCII characters in the source code use Unicode escape sequences (\uXXXX) instead of literal characters. Literal non-ASCII characters occasionally get garbled by a tool or editor.

We have provided a Visual Studio 2013 vssettings file (`stratis.fullnode.vssettings`) at the root of the full node repository, enabling C# auto-formatting conforming to the above guidelines.

### Example File:

``FullNode.cs:``

```
using NBitcoin;
using Stratis.Bitcoin.BlockStore;

namespace Stratis.Bitcoin
{
	public class FullNode : IDisposable
	{
      public Network Network
      {
        get;
        internal set;
      }
    
      public bool IsInitialBlockDownload()
      {
        if (this.ConsensusLoop.Tip == null)
          return true;
        if (this.ConsensusLoop.Tip.ChainWork < this.Network.Consensus.MinimumChainWork)
          return true;
        if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() < (this.DateTimeProvider.GetTime() - this.Args.MaxTipAge))
          return true;
        return false;
      }
  }
}
```

``BlockStoreLoop.cs:``

```
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore
{
	public class BlockPair
	{
		public Block Block;
		public ChainedBlock ChainedBlock;
	}

	public class BlockStoreLoop
	{
		private readonly ConcurrentChain chain;
		private readonly ConnectionManager connection;

		private int batchsize = 30;
		private TimeSpan pushInterval = TimeSpan.FromSeconds(10);
		private readonly TimeSpan pushIntervalIBD = TimeSpan.FromMilliseconds(100);

		public BlockRepository BlockRepository { get; } // public for testing
		private readonly DateTimeProvider dateTimeProvider;
		private readonly NodeArgs nodeArgs;
		private readonly BlockingPuller blockPuller;

		public BlockStore.ChainBehavior.ChainState ChainState
		{
			get;
		}

		public ConcurrentDictionary<uint256, BlockPair> PendingStorage
		{
			get;
		}

		public ChainedBlock StoredBlock
		{
			get; private set;
		}

		public BlockStoreLoop(ConcurrentChain chain, ConnectionManager connection, BlockRepository blockRepository,
			DateTimeProvider dateTimeProvider, NodeArgs nodeArgs, BlockStore.ChainBehavior.ChainState chainState, 
			CancellationTokenSource globalCancellationTokenSource, BlockingPuller blockPuller)
		{
			this.chain = chain;
			this.connection = connection;
			this.BlockRepository = blockRepository;
			this.dateTimeProvider = dateTimeProvider;
			this.nodeArgs = nodeArgs;
			this.blockPuller = blockPuller;
			this.ChainState = chainState;
			
			if(this.nodeArgs.Store.ReIndex)
				throw new NotImplementedException();

			PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();
			StoredBlock = chain.GetBlock(this.BlockRepository.BlockHash);
			if (StoredBlock == null)
			{
				// TODO: load and delete all blocks till a common fork with Chain is found
				// this is a rare event where a reorg happened and the ChainedBlock is lost
				// to solve this each block needs to be pulled from storage and deleted 
				// all the way till a common fork is found with Chain
				throw new NotImplementedException();
			}

			if (this.nodeArgs.Store.TxIndex != this.BlockRepository.TxIndex)
			{
				if (this.chain.Tip != this.chain.Genesis)
					throw new BlockStoreException("You need to rebuild the database using -reindex-chainstate to change -txindex");
				if (this.nodeArgs.Store.TxIndex)
					this.BlockRepository.SetTxIndex(this.nodeArgs.Store.TxIndex);
			}

			chainState.HighestPersistedBlock = this.StoredBlock;
			this.Loop(globalCancellationTokenSource.Token);
		}

		public void AddToPending(Block block)
		{
			var chainedBlock = this.chain.GetBlock(block.GetHash());
			if (chainedBlock == null)
				return; // reorg

			// check the size of pending in memory

			// add to pending blocks
			if (this.StoredBlock.Height < chainedBlock.Height)
				this.PendingStorage.TryAdd(chainedBlock.HashBlock, new BlockPair {Block = block, ChainedBlock = chainedBlock});
		}

		public Task Flush()
		{
			return this.DownloadAndStoreBlocks(CancellationToken.None, true);
		}

		public void Loop(CancellationToken cancellationToken)
		{
			// A loop that writes pending blocks to store 
			// or downloads missing blocks then writes them to store
			AsyncLoop.Run("BlockStoreLoop.DownloadBlocks", async token =>
			{
				await DownloadAndStoreBlocks(cancellationToken);
			},
			cancellationToken,
			repeateEvery: TimeSpans.Second,
			startAfter: TimeSpans.FiveSeconds);
		}

		public async Task DownloadAndStoreBlocks(CancellationToken token, bool disposemode = false)
		{
			while (!token.IsCancellationRequested)
			{
				if (StoredBlock.Height >= this.ChainState.HighestValidatedPoW?.Height)
					break;

				// find next block to download
				var next = this.chain.GetBlock(StoredBlock.Height + 1);
				if (next == null)
					break; //no blocks to store

				// reorg logic
				if (this.StoredBlock.HashBlock != next.Header.HashPrevBlock)
				{
					if(disposemode)
						break;

					var blockstoremove = new List<uint256>();
					var remove = this.StoredBlock;
					// reorg - we need to delete blocks, start walking back the chain
					while (this.chain.GetBlock(remove.HashBlock) == null)
					{
						blockstoremove.Add(remove.HashBlock);
						remove = remove.Previous;
					}

					await this.BlockRepository.DeleteAsync(remove.HashBlock, blockstoremove);
					this.StoredBlock = remove;
					this.ChainState.HighestPersistedBlock = this.StoredBlock;
					break;
				}

				if (await this.BlockRepository.ExistAsync(next.HashBlock))
				{
					// next block is in storage update StoredBlock 
					await this.BlockRepository.SetBlockHash(next.HashBlock);
					this.StoredBlock = next;
					this.ChainState.HighestPersistedBlock = this.StoredBlock;
					continue;
				}

				// check if the next block is in pending storage
				BlockPair insert;
				if (this.PendingStorage.TryGetValue(next.HashBlock, out insert))
				{
					// if in IBD and batch will not be full then wait for more blocks
					if (this.ChainState.IsInitialBlockDownload && !disposemode)
						if (this.PendingStorage.Skip(0).Count() < batchsize) // ConcurrentDictionary perf
							break;

					if (!this.PendingStorage.TryRemove(next.HashBlock, out insert))
						break;

					var tostore = new List<BlockPair>(new[] { insert });
					var storebest = next;
					foreach (var index in Enumerable.Range(1, batchsize - 1))
					{
						var old = next;
						next = this.chain.GetBlock(next.Height + 1);
						
						// stop if at the tip or block is already in store or pending insertion
						if (next == null) break;
						if (next.Header.HashPrevBlock != old.HashBlock) break;
						if (next.Height > this.ChainState.HighestValidatedPoW?.Height) break;
						if (!this.PendingStorage.TryRemove(next.HashBlock, out insert)) break;
						tostore.Add(insert);
						storebest = next;
					}

					// store missing blocks and remove them from pending blocks
					await this.BlockRepository.PutAsync(storebest.HashBlock, tostore.Select(b => b.Block).ToList());
					this.StoredBlock = storebest;
					this.ChainState.HighestPersistedBlock = this.StoredBlock;

					// this can be twicked if insert is effecting the consensus speed
					if (this.ChainState.IsInitialBlockDownload)
						await Task.Delay(pushIntervalIBD, token);

					continue;
				}

				if(disposemode)
					break;

				// block is not in store and not in pending 
				// download the block or blocks
				// find a batch of blocks to download
				var todownload = new List<ChainedBlock>(new[] {next});
				var downloadbest = next;
				foreach (var index in Enumerable.Range(1, batchsize - 1))
				{
					var old = next;
					next = this.chain.GetBlock(old.Height + 1);

					// stop if at the tip or block is already in store or pending insertion
					if (next == null) break;
					if (next.Header.HashPrevBlock != old.HashBlock) break;
					if (next.Height > this.ChainState.HighestValidatedPoW?.Height) break;
					if (this.PendingStorage.ContainsKey(next.HashBlock)) break;
					if (await this.BlockRepository.ExistAsync(next.HashBlock)) break;

					todownload.Add(next);
					downloadbest = next;
				}

				// download and store missing blocks
				var blocks = await this.blockPuller.AskBlocks(token, todownload.ToArray());
				await this.BlockRepository.PutAsync(downloadbest.HashBlock, blocks);
				this.StoredBlock = downloadbest;
				this.ChainState.HighestPersistedBlock = this.StoredBlock;
			}
		}
	}
}

```
