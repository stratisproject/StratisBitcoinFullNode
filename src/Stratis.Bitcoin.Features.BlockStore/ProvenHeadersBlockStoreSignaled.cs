using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class ProvenHeadersBlockStoreSignaled : BlockStoreSignaled
    {
        private readonly Network network;
        private readonly IProvenBlockHeaderStore provenBlockHeaderStore;

        public ProvenHeadersBlockStoreSignaled(
            Network network,
            IBlockStoreQueue blockStoreQueue,
            ConcurrentChain chain,
            StoreSettings storeSettings,
            IChainState chainState,
            IConnectionManager connection,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState initialBlockDownloadState,
            IProvenBlockHeaderStore provenBlockHeaderStore)
            : base(blockStoreQueue, chain, storeSettings, chainState, connection, nodeLifetime, loggerFactory, initialBlockDownloadState)
        {
            this.network = Guard.NotNull(network, nameof(network));
            this.provenBlockHeaderStore = Guard.NotNull(provenBlockHeaderStore, nameof(provenBlockHeaderStore));
        }

        private bool AreProvenHeadersActivated(int blockHeight)
        {
            if (this.network.Consensus.Options is PosConsensusOptions options)
            {
                return blockHeight >= options.ProvenHeadersActivationHeight;
            }

            return false;
        }

        /// <inheritdoc />
        protected override void AddBlockToQueue(ChainedHeaderBlock blockPair)
        {
            base.AddBlockToQueue(blockPair);

            int blockHeight = blockPair.ChainedHeader.Height;
            PosBlock block = blockPair.Block as PosBlock;
            if (block != null && this.AreProvenHeadersActivated(blockHeight))
            {
                uint256 blockHash = blockPair.Block.Header.GetHash();

                ProvenBlockHeader provenHeader = this.provenBlockHeaderStore.GetAsync(blockPair.ChainedHeader.Height).GetAwaiter().GetResult();
                // Proven Header not found? create it now.
                if (provenHeader == null)
                {
                    logger.LogTrace("Proven Header at height {0} NOT found.", blockHeight);
                    CreateAndStoreProvenHeader(blockHeight, block);
                }
                else
                {
                    // If the Proven Header is the right one, then it's OK and we can return without doing anything.
                    uint256 provenHeaderHash = provenHeader.GetHash();
                    if (provenHeaderHash == blockHash)
                    {
                        logger.LogTrace("Proven Header {0} found.", blockHash);
                    }
                    else
                    {
                        logger.LogTrace("Proven Header at height {0} found but hashes don't match, updating the stored Proven Header.", blockHeight);

                        //TODO: does AddToPendingBatch manage itself the removal of a PH at the same height? I doubt, need to investigate
                        CreateAndStoreProvenHeader(blockHeight, block);
                    }
                }
            }
        }

        /// <summary>
        /// Creates the and store a <see cref="ProvenBlockHeader"/>.
        /// </summary>
        /// <param name="blockHeight">Height of the block used to generate its Proven Header.</param>
        /// <param name="block">Block used to generate its Proven Header.</param>
        private void CreateAndStoreProvenHeader(int blockHeight, PosBlock block)
        {
            ProvenBlockHeader newProvenHeader = ((PosConsensusFactory)this.network.Consensus.ConsensusFactory).CreateProvenBlockHeader(block);

            uint256 provenHeaderHash = newProvenHeader.GetHash();
            logger.LogTrace("Creating Proven Header at height {0} with hash {1} and adding to the store.", blockHeight, provenHeaderHash);
            this.provenBlockHeaderStore.AddToPendingBatch(newProvenHeader, new HashHeightPair(provenHeaderHash, blockHeight));
        }
    }
}