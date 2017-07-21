using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
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
    public class BlockPair
    {
        public Block Block;
        public ChainedBlock ChainedBlock;
    }

    public sealed class BlockStoreLoop
    {
        internal readonly ConcurrentChain Chain;
        public BlockRepository BlockRepository { get; } // public for testing
        private readonly NodeSettings nodeArgs;
        internal readonly StoreBlockPuller blockPuller;
        private readonly BlockStoreCache blockStoreCache;
        private readonly BlockStoreStats blockStoreStats;

        public ChainBehavior.ChainState ChainState { get; }

        public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }

        public BlockStoreLoop(
            ConcurrentChain chain,
            BlockRepository blockRepository,
            NodeSettings nodeArgs,
            ChainBehavior.ChainState chainState,
            StoreBlockPuller blockPuller,
            BlockStoreCache cache)
        {
            this.Chain = chain;
            this.BlockRepository = blockRepository;
            this.nodeArgs = nodeArgs;
            this.blockPuller = blockPuller;
            this.ChainState = chainState;
            this.blockStoreCache = cache;

            this.PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();
            this.blockStoreStats = new BlockStoreStats(this.BlockRepository, this.blockStoreCache);
        }

        // downaloading 5mb is not much in case the store need to catchup
        internal uint insertsizebyte = 1000000 * 5; // Block.MAX_BLOCK_SIZE 
        internal int batchtriggersize = 5;
        internal int batchdownloadsize = 1000;
        private TimeSpan pushInterval = TimeSpan.FromSeconds(10);
        internal readonly TimeSpan pushIntervalIBD = TimeSpan.FromMilliseconds(100);
        public ChainedBlock StoredBlock { get; set; }

        public async Task Initialize(CancellationTokenSource cancellationToken)
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

        public void AddToPending(Block block)
        {
            var chainedBlock = this.Chain.GetBlock(block.GetHash());
            if (chainedBlock == null)
                return; // reorg

            // check the size of pending in memory

            // add to pending blocks
            if (this.StoredBlock.Height < chainedBlock.Height)
                this.PendingStorage.TryAdd(chainedBlock.HashBlock, new BlockPair { Block = block, ChainedBlock = chainedBlock });
        }

        public Task Flush()
        {
            return DownloadAndStoreBlocks(CancellationToken.None, true);
        }

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

        public async Task DownloadAndStoreBlocks(CancellationToken cancellationToken, bool disposeMode = false)
        {
            // TODO: add support to BlockStoreLoop to unset LazyLoadingOn when not in IBD
            // When in IBD we may need many reads for the block key without fetching the block
            // So the repo starts with LazyLoadingOn = true, however when not anymore in IBD 
            // a read is normally done when a peer is asking for the entire block (not just the key) 
            // then if LazyLoadingOn = false the read will be faster on the entire block            

            while (!cancellationToken.IsCancellationRequested)
            {
                if (this.StoredBlock.Height >= this.ChainState.HighestValidatedPoW?.Height)
                    break;

                var nextChainedBlock = this.Chain.GetBlock(this.StoredBlock.Height + 1);
                if (nextChainedBlock == null)
                    break;

                if (this.blockStoreStats.CanLog)
                    this.blockStoreStats.Log();

                var steps = new BlockStoreLoopStepChain(this, nextChainedBlock, cancellationToken, disposeMode);
                steps.SetNextStep(new BlockStoreLoopStepReorganise());
                steps.SetNextStep(new BlockStoreLoopStepCheckExists());
                steps.SetNextStep(new BlockStoreLoopStepTryPending());
                steps.SetNextStep(new BlockStoreLoopStepTryDownload());
                var result = await steps.Execute();

                if (result.ShouldBreak)
                    break;
                if (result.ShouldContinue)
                    continue;
            }
        }
    }
}