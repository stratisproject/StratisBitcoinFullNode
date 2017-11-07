using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.IntegrationTests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.MemoryPool.Tests")]

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Information about a block that is required for its validation. 
    /// It is used when a new block is downloaded or mined.
    /// </summary>
    public class BlockValidationContext
    {
        /// <summary>The chain of headers associated with the block.</summary>
        public ChainedBlock ChainedBlock { get; set; }

        /// <summary>Downloaded or mined block to be validated.</summary>
        public Block Block { get; set; }

        /// <summary>If the block validation failed this will be set with the reason of failure.</summary>
        public ConsensusError Error { get; set; }
    }
    
    /// <summary>
    /// A class that is responsible for downloading blocks from peers using the <see cref="ILookaheadBlockPuller"/> 
    /// and validating this blocks using either the <see cref="PowConsensusValidator"/> for POF networks or <see cref="PosConsensusValidator"/> for POS networks. 
    /// </summary>
    /// <remarks>
    /// An internal loop will manage such background operations.
    /// </remarks>
    public class ConsensusLoop
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Information holding POS data chained.</summary>
        public StakeChain StakeChain { get; }
        
        /// <summary>A puller that can pull blocks from peers on demand.</summary>
        public LookaheadBlockPuller Puller { get; }

        /// <summary>A chain of headers all the way to genesis.</summary>
        public ConcurrentChain Chain { get; }

        /// <summary>The consensus db, containing all unspent UTXO in the chain.</summary>
        public CoinView UTXOSet { get; }

        /// <summary>The validation logic for the consensus rules.</summary>
        public PowConsensusValidator Validator { get; }

        /// <summary>The current tip of the cahin that has been validated.</summary>
        public ChainedBlock Tip { get; private set; }

        /// <summary>Contain information about deployment and activation of features in the chain.</summary>
        public NodeDeployments NodeDeployments { get; private set; }

        /// <summary>Factory for creating and also possibly starting application defined tasks inside async loop.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Contain information about the life time of the node, its used on startup and shutdown.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Holds state related to the block chain.</summary>
        private readonly ChainState chainState;

        /// <summary>Connection manager of all the currently connected peers.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>A signaler that used to signal messages between features.</summary>
        private readonly Signals.Signals signals;

        /// <summary>A lock object that synchronizes access to the <see cref="ConsensusLoop.AcceptBlockAsync"/> and the reorg part of <see cref="ConsensusLoop.PullerLoopAsync"/> methods.</summary>
        private readonly AsyncLock consensusLock;

        /// <summary>Provider of block header hash checkpoints.</summary>
        private readonly ICheckpoints checkpoints;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Initialize a new instance of <see cref="ConsensusLoop"/>.
        /// </summary>
        /// <param name="asyncLoopFactory">The async loop we need to wait upon before we can shut down this feature.</param>
        /// <param name="validator">The validation logic for the consensus rules.</param>
        /// <param name="nodeLifetime">Contain information about the life time of the node, its used on startup and shutdown.</param>
        /// <param name="chain">A chain of headers all the way to genesis.</param>
        /// <param name="utxoSet">The consensus db, containing all unspent UTXO in the chain.</param>
        /// <param name="puller">A puller that can pull blocks from peers on demand.</param>
        /// <param name="nodeDeployments">Contain information about deployment and activation of features in the chain.</param>
        /// <param name="loggerFactory">A factory to provide logger instances.</param>
        /// <param name="chainState">Holds state related to the block chain.</param>
        /// <param name="connectionManager">Connection manager of all the currently connected peers.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="signals">A signaler that used to signal messages between features.</param>
        /// <param name="checkpoints">Provider of block header hash checkpoints.</param>
        /// <param name="stakeChain">Information holding POS data chained.</param>
        public ConsensusLoop(
            IAsyncLoopFactory asyncLoopFactory,
            PowConsensusValidator validator,
            INodeLifetime nodeLifetime,
            ConcurrentChain chain, 
            CoinView utxoSet, 
            LookaheadBlockPuller puller, 
            NodeDeployments nodeDeployments, 
            ILoggerFactory loggerFactory,
            ChainState chainState,
            IConnectionManager connectionManager,
            IDateTimeProvider dateTimeProvider,
            Signals.Signals signals,
            ICheckpoints checkpoints,
            StakeChain stakeChain = null)
        {
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(validator, nameof(validator));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(utxoSet, nameof(utxoSet));
            Guard.NotNull(puller, nameof(puller));
            Guard.NotNull(nodeDeployments, nameof(nodeDeployments));
            Guard.NotNull(connectionManager, nameof(connectionManager));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(signals, nameof(signals));

            this.consensusLock = new AsyncLock();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.asyncLoopFactory = asyncLoopFactory;
            this.Validator = validator;
            this.nodeLifetime = nodeLifetime;
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.signals = signals;
            this.Chain = chain;
            this.UTXOSet = utxoSet;
            this.Puller = puller;
            this.NodeDeployments = nodeDeployments;
            this.checkpoints = checkpoints;
            this.dateTimeProvider = dateTimeProvider;

            // chain of stake info can be null if POS is not enabled
            this.StakeChain = stakeChain;
        }

        /// <summary>
        /// Initialize components in <see cref="ConsensusLoop"/>.
        /// </summary>
        public async Task StartAsync()
        {
            this.logger.LogTrace("()");

            uint256 utxoHash = await this.UTXOSet.GetBlockHashAsync().ConfigureAwait(false);
            while (true)
            {
                this.Tip = this.Chain.GetBlock(utxoHash);
                if (this.Tip != null)
                    break;

                // TODO: this rewind code may never happen. 
                // The node will complete loading before connecting to peers so the  
                // chain will never know if a reorg happened.
                utxoHash = await this.UTXOSet.Rewind().ConfigureAwait(false);
            }
            this.Puller.SetLocation(this.Tip);

            this.asyncLoop = this.asyncLoopFactory.Run($"Consensus Loop", async (token) =>
            {
                await this.PullerLoopAsync(this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
            }, 
            this.nodeLifetime.ApplicationStopping, 
            repeatEvery: TimeSpans.RunOnce);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Dispose components in <see cref="ConsensusLoop"/>.
        /// </summary>
        public void Stop()
        {
            this.asyncLoop.Dispose();
        }

        /// <summary>
        /// A puller method that will continuously loop and ask for the next block  in the chain from peers.
        /// The block will then be passed to the consensus validation. 
        /// </summary>
        /// <remarks>
        /// If the <see cref="Block"/> returned from the puller is null that means the puller is signalling a reorg was detected.
        /// In this case a rewind of the <see cref="CoinView"/> db will be triggered to roll back consensus until a block is found that is in the best chain.
        /// </remarks>
        /// <param name="cancellationToken">A cancellation token that will stop the loop.</param>
        private async Task PullerLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                BlockValidationContext blockValidationContext = new BlockValidationContext();

                using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddBlockFetchingTime(o)))
                {
                    // Save the current consensus tip to later check if it changed.
                    ChainedBlock consensusTip = this.Tip;

                    // This method will block until the next block is downloaded.
                    blockValidationContext.Block = this.Puller.NextBlock(cancellationToken);

                    if (blockValidationContext.Block == null)
                    {
                        using (await this.consensusLock.LockAsync().ConfigureAwait(false))
                        {
                            this.logger.LogTrace("No block received from puller due to reorganization.");

                            if (!consensusTip.Equals(this.Tip))
                            {
                                this.logger.LogTrace("Consensus tip changed from '{0}' to '{1}', no rewinding.", consensusTip, this.Tip);
                                continue;
                            }

                            this.logger.LogTrace("Rewinding.");
                            await this.RewindCoinViewLockedAsync().ConfigureAwait(false);

                            continue;
                        }
                    }
                }

                this.logger.LogTrace("Block received from puller.");
                await this.AcceptBlockAsync(blockValidationContext).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Rewinds coinview to a block that exists on the current best chain.
        /// Also resets consensus tip, puller's tip and chain state's consensus tip to that block.
        /// </summary>
        /// <remarks>The caller of this method is responsible for holding <see cref="consensusLock"/>.</remarks>
        private async Task RewindCoinViewLockedAsync()
        {
            this.logger.LogTrace("()");

            ChainedBlock lastTip = this.Tip;
            CancellationToken token = this.nodeLifetime.ApplicationStopping;

            ChainedBlock rewinded = null;
            while (rewinded == null)
            {
                token.ThrowIfCancellationRequested();

                uint256 hash = await this.UTXOSet.Rewind().ConfigureAwait(false);
                rewinded = this.Chain.GetBlock(hash);
                if (rewinded == null)
                {
                    this.logger.LogTrace("Rewound to '{0}', which is still not a part of the current best chain, rewinding further.", hash);
                }
            }

            this.Tip = rewinded;
            this.Puller.SetLocation(rewinded);
            this.chainState.ConsensusTip = this.Tip;
            this.logger.LogInformation("Reorg detected, rewinding from '{0}' to '{1}'.", lastTip, this.Tip);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A method that will accept a new block to the node.
        /// The block will be validated and the <see cref="CoinView"/> db will be updated.
        /// If it's a new block that was mined or staked it will extend the chain and the new block will set <see cref="ConcurrentChain.Tip"/>.
        /// </summary>
        /// <param name="blockValidationContext">Information about the block to validate.</param>
        public async Task AcceptBlockAsync(BlockValidationContext blockValidationContext)
        {
            this.logger.LogTrace("()");

            using (await this.consensusLock.LockAsync().ConfigureAwait(false))
            {
                try
                {
                    await this.ValidateAndExecuteBlockAsync(new ContextInformation(blockValidationContext, this.Validator.ConsensusParams)).ConfigureAwait(false);
                }
                catch (ConsensusErrorException ex)
                {
                    blockValidationContext.Error = ex.ConsensusError;
                }

                if (blockValidationContext.Error != null)
                {
                    uint256 rejectedBlockHash = blockValidationContext.Block.GetHash();
                    this.logger.LogError("Block '{0}' rejected: {1}", rejectedBlockHash, blockValidationContext.Error.Message);

                    // Check if the error is a consensus failure.
                    if (blockValidationContext.Error == ConsensusErrors.InvalidPrevTip)
                    {
                        if (!this.Chain.Contains(this.Tip.HashBlock))
                        {
                            // Our consensus tip is not on the best chain, which means that the current block
                            // we are processing might be rejected only because of that. The consensus is on wrong chain
                            // and need to be reset.
                            await this.RewindCoinViewLockedAsync().ConfigureAwait(false);
                        }

                        this.logger.LogTrace("(-)[INVALID_PREV_TIP]");
                        return;
                    }

                    // Pull again.
                    this.Puller.SetLocation(this.Tip);

                    if (blockValidationContext.Error == ConsensusErrors.BadWitnessNonceSize)
                    {
                        this.logger.LogInformation("You probably need witness information, activating witness requirement for peers.");
                        this.connectionManager.AddDiscoveredNodesRequirement(NodeServices.NODE_WITNESS);
                        this.Puller.RequestOptions(TransactionOptions.Witness);

                        this.logger.LogTrace("(-)[BAD_WITNESS_NONCE_SIZE]");
                        return;
                    }

                    // Set the chain back to ConsensusLoop.Tip.
                    this.Chain.SetTip(this.Tip);
                    this.logger.LogTrace("Chain reverted back to block '{0}'.", this.Tip);

                    // Since ChainHeadersBehavior check PoW, MarkBlockInvalid can't be spammed.
                    this.logger.LogError("Marking block '{0}' as invalid.", rejectedBlockHash);
                    this.chainState.MarkBlockInvalid(rejectedBlockHash);
                }
                else
                {
                    this.logger.LogTrace("Block '{0}' accepted.", this.Tip);

                    this.chainState.ConsensusTip = this.Tip;

                    // We really want to flush if we are at the top of the chain.
                    // Otherwise, we just allow the flush to happen if it is needed.
                    bool forceFlush = this.Chain.Tip.HashBlock == blockValidationContext.ChainedBlock?.HashBlock;
                    await this.FlushAsync(forceFlush).ConfigureAwait(false);

                    if (this.Tip.ChainWork > this.Chain.Tip.ChainWork)
                    {
                        // This is a newly mined block.
                        this.Chain.SetTip(this.Tip);
                        this.Puller.SetLocation(this.Tip);

                        this.logger.LogDebug("Block extends best chain tip to '{0}'.", this.Tip);
                    }

                    this.signals.SignalBlock(blockValidationContext.Block);
                }
            }

            this.logger.LogTrace("(-):*.{0}='{1}',*.{2}='{3}'", nameof(blockValidationContext.ChainedBlock), blockValidationContext.ChainedBlock, nameof(blockValidationContext.Error), blockValidationContext.Error?.Message);
        }

        /// <summary>
        /// Validates a block using the consensus rules.
        /// </summary>
        public void ValidateBlock(ContextInformation context)
        {
            this.logger.LogTrace("()");

            using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddBlockProcessingTime(o)))
            {
                // Check that the current block has not been reorged.
                // Catching a reorg at this point will not require a rewind.
                if (context.BlockValidationContext.Block.Header.HashPrevBlock != this.Tip.HashBlock)
                {
                    this.logger.LogTrace("Reorganization detected.");
                    ConsensusErrors.InvalidPrevTip.Throw();
                }

                this.logger.LogTrace("Validating new block.");

                // Build the next block in the chain of headers. The chain header is most likely already created by 
                // one of the peers so after we create a new chained block (mainly for validation) 
                // we ask the chain headers for its version (also to prevent memory leaks). 
                context.BlockValidationContext.ChainedBlock = new ChainedBlock(context.BlockValidationContext.Block.Header, context.BlockValidationContext.Block.Header.GetHash(), this.Tip);

                // Liberate from memory the block created above if possible.
                context.BlockValidationContext.ChainedBlock = this.Chain.GetBlock(context.BlockValidationContext.ChainedBlock.HashBlock) ?? context.BlockValidationContext.ChainedBlock;
                context.SetBestBlock(this.dateTimeProvider.GetTimeOffset());

                // == Validation flow ==

                // Check the block header is correct.
                this.Validator.CheckBlockHeader(context);
                this.Validator.ContextualCheckBlockHeader(context);

                // Calculate the consensus flags and check they are valid.
                context.Flags = this.NodeDeployments.GetFlags(context.BlockValidationContext.ChainedBlock);

                int lastCheckpointHeight = this.checkpoints.GetLastCheckpointHeight();
                if (context.BlockValidationContext.ChainedBlock.Height > lastCheckpointHeight)
                {
                    this.Validator.ContextualCheckBlock(context);

                    // Check the block itself.
                    this.Validator.CheckBlock(context);
                }
                else this.logger.LogTrace("Block validation partially skipped because block height {0} is not greater than last checkpointed block height {1}.", context.BlockValidationContext.ChainedBlock.Height, lastCheckpointHeight);
            }

            this.logger.LogTrace("(-)[OK]");
        }

        /// <summary>
        /// Validates a block using the consensus rules and executes it (processes it and adds it as a tip to consensus).
        /// </summary>
        /// <param name="context">A context that contains all information required to validate the block.</param>
        internal async Task ValidateAndExecuteBlockAsync(ContextInformation context)
        {
            this.logger.LogTrace("()");

            this.ValidateBlock(context);

            // Load the UTXO set of the current block. UTXO may be loaded from cache or from disk.
            // The UTXO set is stored in the context.
            this.logger.LogTrace("Loading UTXO set of the new block.");
            context.Set = new UnspentOutputSet();
            using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddUTXOFetchingTime(o)))
            {
                uint256[] ids = GetIdsToFetch(context.BlockValidationContext.Block, context.Flags.EnforceBIP30);
                FetchCoinsResponse coins = await this.UTXOSet.FetchCoinsAsync(ids).ConfigureAwait(false);
                context.Set.SetCoins(coins.UnspentOutputs);
            }

            // Attempt to load into the cache the next set of UTXO to be validated.
            // The task is not awaited so will not stall main validation process.
            this.TryPrefetchAsync(context.Flags);

            // Validate the UTXO set is correctly spent.
            this.logger.LogTrace("Executing block.");
            using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddBlockProcessingTime(o)))
            {
                this.Validator.ExecuteBlock(context, null);
            }

            // Persist the changes to the coinview. This will likely only be stored in memory, 
            // unless the coinview treashold is reached.
            this.logger.LogTrace("Saving coinview changes.");
            await this.UTXOSet.SaveChangesAsync(context.Set.GetCoins(this.UTXOSet), null, this.Tip.HashBlock, context.BlockValidationContext.ChainedBlock.HashBlock).ConfigureAwait(false);

            // Set the new tip.
            this.Tip = context.BlockValidationContext.ChainedBlock;
            this.logger.LogTrace("(-)[OK]");
        }

        /// <summary>
        /// Flushes changes in the cached coinview to the disk.
        /// </summary>
        /// <param name="force"><c>true</c> to enforce flush, <c>false</c> to flush only if the cached coinview itself wants to be flushed.</param>
        public async Task FlushAsync(bool force)
        {
            this.logger.LogTrace("({0}:{1})", nameof(force), force);

            if (this.UTXOSet is CachedCoinView cachedCoinView)
                await cachedCoinView.FlushAsync(force).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// This method try to load from cache the UTXO of the next block in a background task. 
        /// </summary>
        /// <param name="flags">Information about activated features.</param>
        private async void TryPrefetchAsync(DeploymentFlags flags)
        {
            this.logger.LogTrace("({0}:{1})", nameof(flags), flags);

            if (this.UTXOSet is CachedCoinView)
            {
                Block nextBlock = this.Puller.TryGetLookahead(0);
                if (nextBlock != null)
                    await this.UTXOSet.FetchCoinsAsync(GetIdsToFetch(nextBlock, flags.EnforceBIP30)).ConfigureAwait(false);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Get transaction identifiers to try to pre-fetch them from cache.
        /// </summary>
        /// <param name="block">The block containing transactions to fetch.</param>
        /// <param name="enforceBIP30"><c>true</c> to enforce BIP30.</param>
        /// <returns>List of transaction ids.</returns>
        public uint256[] GetIdsToFetch(Block block, bool enforceBIP30)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(block), block.GetHash(), nameof(enforceBIP30), enforceBIP30);

            HashSet<uint256> ids = new HashSet<uint256>();
            foreach (Transaction tx in block.Transactions)
            {
                if (enforceBIP30)
                {
                    var txId = tx.GetHash();
                    ids.Add(txId);
                }

                if (!tx.IsCoinBase)
                {
                    foreach (TxIn input in tx.Inputs)
                    {
                        ids.Add(input.PrevOut.Hash);
                    }
                }
            }

            uint256[] res = ids.ToArray();
            this.logger.LogTrace("(-):*.{0}={1}", nameof(res.Length), res.Length);
            return res;
        }
    }
}
