using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
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
            if (this.AreProvenHeadersActivated(blockHeight))
            {

                if (blockPair.ChainedHeader.Header is ProvenBlockHeader phHeader)
                {
                    logger.LogTrace("Current header is already a Proven Header.");
                    return;
                }

                ProvenBlockHeader provenHeader = this.provenBlockHeaderStore.GetAsync(blockPair.ChainedHeader.Height).GetAwaiter().GetResult();
                // Proven Header not found? create it now.
                if (provenHeader == null)
                {
                    logger.LogTrace("Proven Header at height {0} NOT found.", blockHeight);

                    var createdProvenHeader = CreateAndStoreProvenHeader(blockHeight, (PosBlock)blockPair.Block);

                    // setters aren't accessible, not sure setting them to public is a nice idea.
                    //blockPair.Block.Header = blockPair.ChainedHeader.Header = createdProvenHeader;
                }
                else
                {
                    uint256 blockHash = blockPair.Block.Header.GetHash();

                    // If the Proven Header is the right one, then it's OK and we can return without doing anything.
                    uint256 provenHeaderHash = provenHeader.GetHash();
                    if (provenHeaderHash == blockHash)
                    {
                        logger.LogTrace("Proven Header {0} found.", blockHash);
                        return;
                    }
                    else
                    {
                        throw new BlockStoreException("Found a proven header with a different hash.");
                    }
                }
            }
        }

        /// <summary>
        /// Creates the and store a <see cref="ProvenBlockHeader" />.
        /// </summary>
        /// <param name="blockHeight">Height of the block used to generate its Proven Header.</param>
        /// <param name="block">Block used to generate its Proven Header.</param>
        /// <returns>Created <see cref="ProvenBlockHeader"/>.</returns>
        private ProvenBlockHeader CreateAndStoreProvenHeader(int blockHeight, PosBlock block)
        {
            ProvenBlockHeader newProvenHeader = ((PosConsensusFactory)this.network.Consensus.ConsensusFactory).CreateProvenBlockHeader(block);

            uint256 provenHeaderHash = newProvenHeader.GetHash();
            this.provenBlockHeaderStore.AddToPendingBatch(newProvenHeader, new HashHeightPair(provenHeaderHash, blockHeight));

            logger.LogTrace("Created Proven Header at height {0} with hash {1} and adding to the store (pending).", blockHeight, provenHeaderHash);

            return newProvenHeader;
        }
    }
}