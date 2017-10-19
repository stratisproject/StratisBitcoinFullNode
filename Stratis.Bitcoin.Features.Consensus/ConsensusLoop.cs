using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// A block object that is passed around when downloading a block from peers and has information about the validation state of the block.
    /// </summary>
    public class BlockItem
    {
        /// <summary>
        /// The chain of headers associated with the current block.
        /// </summary>
        public ChainedBlock ChainedBlock { get; set; }

        /// <summary>
        /// The block downloaded from peers.
        /// </summary>
        public Block Block { get; set; }

        /// <summary>
        /// If the block validation failed this will be set with the reason of failure.
        /// </summary>
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

        /// <summary>A lock object that synchronizes access to the <see cref="ConsensusLoop.AcceptBlock"/> method.</summary>
        private readonly object consensusLock;

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
        /// <param name="signals">A signaler that used to signal messages between features.</param>
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
            Signals.Signals signals,

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

            this.consensusLock = new object();

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

            // chain of stake info can be null if POS is not enabled
            this.StakeChain = stakeChain;
        }

        /// <summary>
        /// Initialize components in <see cref="ConsensusLoop"/>.
        /// </summary>
        public void Start()
        {
            this.logger.LogTrace("()");

            uint256 utxoHash = this.UTXOSet.GetBlockHashAsync().GetAwaiter().GetResult();
            while (true)
            {
                this.Tip = this.Chain.GetBlock(utxoHash);
                if (this.Tip != null)
                    break;

                // TODO: this rewind code may never happen. 
                // The node will complete loading before connecting to peers so the  
                // chain will never know if a reorg happened.
                utxoHash = this.UTXOSet.Rewind().GetAwaiter().GetResult();
            }
            this.Puller.SetLocation(this.Tip);

            this.asyncLoop = this.asyncLoopFactory.Run($"Consensus Loop", async token =>
            {
                await this.PullerLoop(this.nodeLifetime.ApplicationStopping);
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
            this.asyncLoop?.Dispose();
        }

        /// <summary>
        /// A puller method that will continuously loop and ask for the next block  in the chain from peers.
        /// The block will then be passed to the consensus validation. 
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that will stop the loop.</param>
        /// <returns>The task the loop will be running under.</returns>
        private Task PullerLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    BlockItem item = new BlockItem();

                    using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddBlockFetchingTime(o)))
                    {
                        item.Block = this.Puller.NextBlock(cancellationToken);
                    }

                    this.logger.LogTrace("Block received from puller.");
                    this.AcceptBlock(item);

                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                    {
                        if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                            return Task.FromException(ex);
                    }

                    // TODO Need to revisit unhandled exceptions in a way that any process can signal an exception has been
                    // thrown so that the node and all the disposables can stop gracefully.
                    this.logger.LogCritical(new EventId(0), ex, "Consensus loop at Tip:{0} unhandled exception {1}", this.Tip, ex.ToString());
                    NLog.LogManager.Flush();
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// A method that will accept a new block to the node.
        /// The block will be validated and the <see cref="CoinView"/> db will be updated.
        /// If its a new block that extends the chain then the <see cref="ConcurrentChain.Tip"/> will be set.
        /// </summary>
        /// <param name="item">The block to validate</param>
        public void AcceptBlock(BlockItem item)
        {
            this.logger.LogTrace("()");

            lock (this.consensusLock)
            {
                if (item.Block == null)
                {
                    this.logger.LogTrace("No block received from puller due to reorganization, rewinding.");
                    ChainedBlock lastTip = this.Tip;

                    var token = this.nodeLifetime.ApplicationStopping;

                    while (true)
                    {
                        token.ThrowIfCancellationRequested();

                        uint256 hash = this.UTXOSet.Rewind().GetAwaiter().GetResult();
                        ChainedBlock rewinded = this.Chain.GetBlock(hash);
                        if (rewinded == null)
                        {
                            this.logger.LogTrace("Rewound to '{0}', which is still not a part of the current best chain, rewinding further.",
                                hash);
                            continue;
                        }

                        this.Tip = rewinded;
                        this.Puller.SetLocation(rewinded);
                        this.logger.LogInformation("Reorg detected, rewinding from '{0}' to '{1}'.", lastTip, this.Tip);
                        return;
                    }
                }

                try
                {
                    this.ValidateBlock(new ContextInformation(item, this.Validator.ConsensusParams));
                }
                catch (ConsensusErrorException ex)
                {
                    item.Error = ex.ConsensusError;
                }

                if (item.Error != null)
                {
                    this.logger.LogError("Block rejected: {0}", item.Error.Message);

                    // Pull again.
                    this.Puller.SetLocation(this.Tip);

                    if (item.Error == ConsensusErrors.BadWitnessNonceSize)
                    {
                        this.logger.LogInformation("You probably need witness information, activating witness requirement for peers.");
                        this.connectionManager.AddDiscoveredNodesRequirement(NodeServices.NODE_WITNESS);
                        this.Puller.RequestOptions(TransactionOptions.Witness);
                        return;
                    }

                    // Set the chain back to ConsensusLoop.Tip.
                    this.Chain.SetTip(this.Tip);
                    this.logger.LogTrace("Chain reverted back to block '{0}' ", this.Tip);

                    // Since ChainHeadersBehavior check PoW, MarkBlockInvalid can't be spammed.
                    this.logger.LogError("Marking block as invalid.");
                    this.chainState.MarkBlockInvalid(item.Block?.GetHash());
                }
                else
                {
                    this.logger.LogTrace("Block accepted '{0}' ", this.Tip);

                    this.chainState.HighestValidatedPoW = this.Tip;

                    // We really want to flush if we are at the top of the chain.
                    // Otherwise, we just allow the flush to happen if it is needed.
                    bool forceFlush = this.Chain.Tip.HashBlock == item.ChainedBlock?.HashBlock;
                    this.FlushAsync(forceFlush).GetAwaiter().GetResult();

                    if (this.Tip.ChainWork > this.Chain.Tip.ChainWork)
                    {
                        // This is a newly mined block.
                        this.Puller.SetLocation(this.Tip);
                        this.Chain.SetTip(this.Tip);

                        this.logger.LogTrace("Block extends consensus tip to '{0}'", this.Tip);
                    }

                    this.signals.SignalBlock(item.Block);
                }
            }

            this.logger.LogTrace("(-):*.{0}='{1}',*.{2}='{3}'", nameof(item.ChainedBlock), item.ChainedBlock, nameof(item.Error), item.Error?.Message);
        }

        /// <summary>
        /// Validate a block using the consensus rules.
        /// </summary>
        /// <remarks>
        /// WARNING: This method can only be called from <see cref="ConsensusLoop.AcceptBlock"/>, 
        /// it the requirement is to validate a block without affecting the consensus db then <see cref="ContextInformation.OnlyCheck"/> must be set to true.
        /// </remarks>
        /// <param name="context">A context that contains all information required to validate the block.</param>
        public void ValidateBlock(ContextInformation context)
        {
            this.logger.LogTrace("()");

            using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddBlockProcessingTime(o)))
            {
                // Check that the current block has not been reorged.
                // Catching a reorg at this point will not require a rewind.
                if (context.BlockItem.Block.Header.HashPrevBlock != this.Tip.HashBlock)
                {
                    this.logger.LogTrace("Reorganization detected.");
                    ConsensusErrors.InvalidPrevTip.Throw(); // reorg
                }

                this.logger.LogTrace("Validating new block.");

                // Build the next block in the chain of headers. The chain header is most likely already created by 
                // one of the peers so after we create a new chained block (mainly for validation) 
                // we ask the chain headers for its version (also to prevent memory leaks). 
                context.BlockItem.ChainedBlock = new ChainedBlock(context.BlockItem.Block.Header, context.BlockItem.Block.Header.GetHash(), this.Tip);
                
                // Liberate from memory the block created above if possible.
                context.BlockItem.ChainedBlock = this.Chain.GetBlock(context.BlockItem.ChainedBlock.HashBlock) ?? context.BlockItem.ChainedBlock;
                context.SetBestBlock();

                // == validation flow ==
                
                // Check the block header is correct.
                this.Validator.CheckBlockHeader(context);
                this.Validator.ContextualCheckBlockHeader(context);

                // Calculate the consensus flags and check they are valid.
                context.Flags = this.NodeDeployments.GetFlags(context.BlockItem.ChainedBlock);
                this.Validator.ContextualCheckBlock(context);

                // check the block itself
                this.Validator.CheckBlock(context);
            }

            if (context.OnlyCheck)
            {
                this.logger.LogTrace("(-)[CHECK_ONLY]");
                return;
            }

            // Load the UTXO set of the current block. UTXO may be loaded from cache or from disk.
            // The UTXO set is stored in the context.
            this.logger.LogTrace("Loading UTXO set of the new block.");
            context.Set = new UnspentOutputSet();
            using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddUTXOFetchingTime(o)))
            {
                uint256[] ids = GetIdsToFetch(context.BlockItem.Block, context.Flags.EnforceBIP30);
                FetchCoinsResponse coins = this.UTXOSet.FetchCoinsAsync(ids).GetAwaiter().GetResult();
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
            this.UTXOSet.SaveChangesAsync(context.Set.GetCoins(this.UTXOSet), null, this.Tip.HashBlock, context.BlockItem.ChainedBlock.HashBlock).GetAwaiter().GetResult();

            // Set the new tip.
            this.Tip = context.BlockItem.ChainedBlock;
            this.logger.LogTrace("(-)[OK]");
        }

        /// <summary>
        /// Flushes changes in the cached coinview to the disk.
        /// </summary>
        /// <param name="force"><c>true</c> to enforce flush, <c>false</c> to flush only if the cached coinview itself wants to be flushed.</param>
        public async Task FlushAsync(bool force)
        {
            this.logger.LogTrace("({0}:{1})", nameof(force), force);

            await (this.UTXOSet as CachedCoinView)?.FlushAsync(force);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// This method try to load from cash the items of the next block, in a background task. 
        /// </summary>
        /// <param name="flags">A flag to provide information about activated features.</param>
        /// <returns>The process task.</returns>
        private Task TryPrefetchAsync(DeploymentFlags flags)
        {
            this.logger.LogTrace("({0}:{1})", nameof(flags), flags);

            Task prefetching = Task.FromResult<bool>(true);

            if (this.UTXOSet is CachedCoinView)
            {
                Block nextBlock = this.Puller.TryGetLookahead(0);
                if (nextBlock != null)
                    prefetching = this.UTXOSet.FetchCoinsAsync(GetIdsToFetch(nextBlock, flags.EnforceBIP30));
            }

            this.logger.LogTrace("(-)");
            return prefetching;
        }

        /// <summary>
        /// Get transaction identifiers to try to pref-etch them from cache.
        /// </summary>
        /// <param name="block">The block containing transactions to fetch.</param>
        /// <param name="enforceBIP30">The BIP30 flag.</param>
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
