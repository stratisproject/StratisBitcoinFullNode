using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore.LoopSteps;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore
{
    public sealed class BlockStoreLoop
    {
        internal readonly int BatchDownloadSize = 1000; //should be configurable
        public BlockRepository BlockRepository { get; }
        internal readonly StoreBlockPuller StoreBlockPuller;
        internal readonly ConcurrentChain Chain;
        public ChainBehavior.ChainState ChainState { get; }
        internal readonly uint InsertBlockSizeThreshold = 1024; //should be configurable // Block.MAX_BLOCK_SIZE // downaloading 5mb is not much in case the store need to catchup
        public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }
        internal readonly int PendingStorageBatchThreshold = 5; //should be configurable
        internal readonly TimeSpan PushIntervalIBD = TimeSpan.FromMilliseconds(100); //should be configurable
        public ChainedBlock StoredBlock { get; set; }

        private readonly NodeSettings nodeArgs;
        private readonly BlockStoreCache blockStoreCache;
        private readonly BlockStoreStats blockStoreStats;
        private readonly TimeSpan pushInterval = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Public constructor for unit testing
        /// </summary>
        public BlockStoreLoop(
            BlockStoreCache blockStoreCache,
            BlockRepository blockRepository,
            ChainBehavior.ChainState chainState,
            ConcurrentChain chain,
            NodeSettings nodeArgs,
            StoreBlockPuller storeBlockPuller)
        {
            this.BlockRepository = blockRepository;
            this.blockStoreCache = blockStoreCache;
            this.blockStoreStats = new BlockStoreStats(this.BlockRepository, this.blockStoreCache);
            this.Chain = chain;
            this.ChainState = chainState;
            this.nodeArgs = nodeArgs;
            this.PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();
            this.StoreBlockPuller = storeBlockPuller;
        }

        internal async Task Initialize(CancellationTokenSource cancellationToken)
        {
            if (this.nodeArgs.Store.ReIndex)
                throw new NotImplementedException();

            this.StoredBlock = this.Chain.GetBlock(this.BlockRepository.BlockHash);

            if (this.StoredBlock == null)
            {
                // the store is out of sync, this can happen if the node crashed 
                // or was not closed down properly and bestchain tip is not 
                // the same as in store tip, to recover we walk back the chain til  
                // a common block header is found and set the block store tip to that

                var blockstoreResetList = new List<uint256>();
                var resetBlock = await this.BlockRepository.GetAsync(this.BlockRepository.BlockHash);
                var resetBlockHash = resetBlock.GetHash();

                // walk back the chain and find the common block
                while (this.Chain.GetBlock(resetBlockHash) == null)
                {
                    blockstoreResetList.Add(resetBlockHash);
                    if (resetBlock.Header.HashPrevBlock == this.Chain.Genesis.HashBlock)
                    {
                        resetBlockHash = this.Chain.Genesis.HashBlock;
                        break;
                    }
                    resetBlock = await this.BlockRepository.GetAsync(resetBlock.Header.HashPrevBlock);
                    Guard.NotNull(resetBlock, nameof(resetBlock));
                    resetBlockHash = resetBlock.GetHash();
                }

                var newTip = this.Chain.GetBlock(resetBlockHash);
                await this.BlockRepository.DeleteAsync(newTip.HashBlock, blockstoreResetList);
                this.StoredBlock = newTip;
                Logs.BlockStore.LogWarning($"BlockStore Initialize recovering to block height = {newTip.Height} hash = {newTip.HashBlock}");
            }

            if (this.nodeArgs.Store.TxIndex != this.BlockRepository.TxIndex)
            {
                if (this.StoredBlock != this.Chain.Genesis)
                    throw new BlockStoreException("You need to rebuild the database using -reindex-chainstate to change -txindex");
                if (this.nodeArgs.Store.TxIndex)
                    await this.BlockRepository.SetTxIndex(this.nodeArgs.Store.TxIndex);
            }

            this.ChainState.HighestPersistedBlock = this.StoredBlock;

            Loop(cancellationToken.Token);
        }

        internal void AddToPending(Block block)
        {
            var chainedBlock = this.Chain.GetBlock(block.GetHash());
            if (chainedBlock == null)
                return; // reorg

            // check the size of pending in memory

            // add to pending blocks
            if (this.StoredBlock.Height < chainedBlock.Height)
                this.PendingStorage.TryAdd(chainedBlock.HashBlock, new BlockPair(block, chainedBlock));
        }

        internal Task Flush()
        {
            return DownloadAndStoreBlocks(CancellationToken.None, true);
        }

        /// <summary>
        /// A loop that:
        ///     1: Write pending blocks to store 
        ///     2: Download missing blocks and write to store
        /// </summary>
        internal void Loop(CancellationToken cancellationToken)
        {
            AsyncLoop.Run("BlockStoreLoop.DownloadAndStoreBlocks",
                async token => { await DownloadAndStoreBlocks(cancellationToken); },
                cancellationToken,
                repeatEvery: TimeSpans.Mls100,
                startAfter: TimeSpans.FiveSeconds);
        }

        private async Task DownloadAndStoreBlocks(CancellationToken cancellationToken, bool disposeMode = false)
        {
            // TODO: add support to BlockStoreLoop to unset LazyLoadingOn when not in IBD
            // When in IBD we may need many reads for the block key without fetching the block
            // So the repo starts with LazyLoadingOn = true, however when not anymore in IBD 
            // a read is normally done when a peer is asking for the entire block (not just the key) 
            // then if LazyLoadingOn = false the read will be faster on the entire block            

            var steps = new BlockStoreLoopStepChain(disposeMode);
            steps.SetNextStep(new ReorganiseBlockRepositoryStep(this));
            steps.SetNextStep(new CheckNextChainedBlockExistStep(this));
            steps.SetNextStep(new ProcessPendingStorageStep(this));
            steps.SetNextStep(new DownloadBlockStep(this));

            while (!cancellationToken.IsCancellationRequested)
            {
                if (this.StoredBlock.Height >= this.ChainState.HighestValidatedPoW?.Height)
                    break;

                var nextChainedBlock = this.Chain.GetBlock(this.StoredBlock.Height + 1);
                if (nextChainedBlock == null)
                    break;

                if (this.blockStoreStats.CanLog)
                    this.blockStoreStats.Log();

                var result = await steps.Execute(nextChainedBlock, cancellationToken);
                if (result.ShouldBreak)
                    break;
                if (result.ShouldContinue)
                    continue;
            }

            steps = null;
        }
    }
}