using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Utilities;

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
		public BlockRepository BlockRepository { get; } // public for testing
		private readonly NodeArgs nodeArgs;
		private readonly BlockingPuller blockPuller;
		public BlockStore.ChainBehavior.ChainState ChainState { get; }

		public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }

		public BlockStoreLoop(ConcurrentChain chain, BlockRepository blockRepository, NodeArgs nodeArgs,
			BlockStore.ChainBehavior.ChainState chainState,
			FullNode.CancellationProvider cancellationProvider, BlockingPuller blockPuller)
		{
			this.chain = chain;
			this.BlockRepository = blockRepository;
			this.nodeArgs = nodeArgs;
			this.blockPuller = blockPuller;
			this.ChainState = chainState;

			PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();
			this.Initialize(cancellationProvider.Cancellation).Wait(); // bad practice 
		}

		// downaloading 5mb is not much in case the store need to catchup
		private uint insertsizebyte = 1000000 * 5; // Block.MAX_BLOCK_SIZE 
		private int batchtriggersize = 5;
		private int batchdownloadsize = 30;
		private TimeSpan pushInterval = TimeSpan.FromSeconds(10);
		private readonly TimeSpan pushIntervalIBD = TimeSpan.FromMilliseconds(100);

		public async Task Initialize(CancellationTokenSource tokenSource)
		{
			if (this.nodeArgs.Store.ReIndex)
				throw new NotImplementedException();

			StoredBlock = chain.GetBlock(this.BlockRepository.BlockHash);
			if (StoredBlock == null)
			{
				// a reorg happened and the ChainedBlock is lost
				// to solve this each block needs to be pulled from storage and deleted 
				// all the way till a common fork is found with Chain

				var blockstoremove = new List<uint256>();
				var remove = await this.BlockRepository.GetAsync(this.BlockRepository.BlockHash);
				var removeHash = remove.GetHash();
				// reorg - we need to delete blocks, start walking back the chain
				while (this.chain.GetBlock(removeHash) == null)
				{
					blockstoremove.Add(removeHash);
					if (remove.Header.HashPrevBlock == chain.Genesis.HashBlock)
					{
						removeHash = chain.Genesis.HashBlock;
						break;
					}
					remove = await this.BlockRepository.GetAsync(remove.Header.HashPrevBlock);
					Guard.NotNull(remove, nameof(remove));
					removeHash = remove.GetHash();
				}

				var newTip = this.chain.GetBlock(removeHash);
				await this.BlockRepository.DeleteAsync(newTip.HashBlock, blockstoremove);
				this.StoredBlock = newTip;
			}

			if (this.nodeArgs.Store.TxIndex != this.BlockRepository.TxIndex)
			{
				if (this.StoredBlock != this.chain.Genesis)
					throw new BlockStoreException("You need to rebuild the database using -reindex-chainstate to change -txindex");
				if (this.nodeArgs.Store.TxIndex)
					await this.BlockRepository.SetTxIndex(this.nodeArgs.Store.TxIndex);
			}

			this.ChainState.HighestPersistedBlock = this.StoredBlock;
			this.Loop(tokenSource.Token);
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

		public ChainedBlock StoredBlock { get; private set; }

		public void Loop(CancellationToken cancellationToken)
		{
			// A loop that writes pending blocks to store 
			// or downloads missing blocks then writing to store
			AsyncLoop.Run("BlockStoreLoop.DownloadBlocks", async token =>
			{
				await DownloadAndStoreBlocks(cancellationToken);
			},
			cancellationToken,
			repeatEvery: TimeSpans.Second,
			startAfter: TimeSpans.FiveSeconds);
		}

		public async Task DownloadAndStoreBlocks(CancellationToken token, bool disposemode = false)
		{
			// TODO: add support to BlockStoreLoop to unset LazyLoadingOn when not in IBD
			// When in IBD we may need many reads for the block key without fetching the block
			// So the repo starts with LazyLoadingOn = true, however when not anymore in IBD 
			// a read is normally done when a peer is asking for the entire block (not just the key) 
			// then if LazyLoadingOn = false the read will be faster on the entire block

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
					if (disposemode)
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
					// if in IBD and batch is not full then wait for more blocks
					if (this.ChainState.IsInitialBlockDownload && !disposemode)
						if (this.PendingStorage.Skip(0).Count() < batchtriggersize) // ConcurrentDictionary perf
							break;

					if (!this.PendingStorage.TryRemove(next.HashBlock, out insert))
						break;

					var tostore = new List<BlockPair>(new[] { insert });
					var storebest = next;
					var insertSize = insert.Block.GetSerializedSize();
					while (insertSize < insertsizebyte)
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
						insertSize += insert.Block.GetSerializedSize(); // TODO: add the size to the result coming from the signaler
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

				if (disposemode)
					break;

				// block is not in store and not in pending 
				// download the block or blocks
				// find a batch of blocks to download
				var todownload = new List<ChainedBlock>(new[] {next});
				var downloadbest = next;
				foreach (var index in Enumerable.Range(1, batchdownloadsize - 1))
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
