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
		private readonly ConnectionManager connection;
		public BlockRepository BlockRepository { get; } // public for testing
		private readonly DateTimeProvider dateTimeProvider;
		private readonly NodeArgs nodeArgs;
		private readonly BlockingPuller blockPuller;
		public BlockStore.ChainBehavior.ChainState ChainState { get; }

		public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }

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

			PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();

			this.Initialize(globalCancellationTokenSource).Wait(); // bad practice 
		}

		private int batchsize = 30;
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
				// reorg - we need to delete blocks, start walking back the chain
				while (this.chain.GetBlock(remove.GetHash()) == null)
				{
					blockstoremove.Add(remove.GetHash());
					remove = await this.BlockRepository.GetAsync(remove.Header.HashPrevBlock);
				}

				var newTip = this.chain.GetBlock(remove.GetHash());
				await this.BlockRepository.DeleteAsync(newTip.HashBlock, blockstoremove);
				this.StoredBlock = newTip;
			}

			if (this.nodeArgs.Store.TxIndex != this.BlockRepository.TxIndex)
			{
				if (this.chain.Tip != this.chain.Genesis)
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
