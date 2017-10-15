﻿using System;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class BlockItem
    {
        public ChainedBlock ChainedBlock { get; set; }
        public Block Block { get; set; }
        public ConsensusError Error { get; set; }
    }
    
    public class ConsensusLoop
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Information holding POS data chained.</summary>
        public StakeChain StakeChain { get; }
        
        /// <summary>A puller that can pull blocks from peers on demand.</summary>
        public LookaheadBlockPuller Puller { get; }

        /// <summary>A chain of headers all the way to gensis.</summary>
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

        /// <summary>Contain information about the life time of the node, its used on startup and shuitdown.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly ChainState chainState;
        private readonly IConnectionManager connectionManager;
        private readonly Signals.Signals signals;

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

        public void Stop()
        {
            this.asyncLoop?.Dispose();
        }

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
                    this.logger.LogCritical(new EventId(0), ex, "Consensus loop at Tip:{0} unhandled exception {1}", this.Tip?.Height, ex.ToString());
                    NLog.LogManager.Flush();
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        public void AcceptBlock(BlockItem item)
        {
            this.logger.LogTrace("()");

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
                        this.logger.LogTrace("Rewound to '{0}', which is still not a part of the current best chain, rewinding further.", hash);
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

                // Since ChainHeadersBehavior check PoW, MarkBlockInvalid can't be spammed.
                this.logger.LogError("Marking block as invalid.");
                this.chainState.MarkBlockInvalid(item.Block?.GetHash());
            }
            else
            {
                this.chainState.HighestValidatedPoW = this.Tip;
                if (this.Chain.Tip.HashBlock == item.ChainedBlock?.HashBlock)
                    this.FlushAsync().GetAwaiter().GetResult();

                this.signals.SignalBlock(item.Block);
            }

            this.logger.LogTrace("(-):*.{0}='{1}/{2}',*.{3}='{4}'", nameof(item.ChainedBlock), item.ChainedBlock?.HashBlock, item.ChainedBlock?.Height, nameof(item.Error), item.Error?.Message);
        }

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

        public Task FlushAsync()
        {
            this.logger.LogTrace("()");

            Task res = (this.UTXOSet as CachedCoinView)?.FlushAsync();

            this.logger.LogTrace("(-)");
            return res;
        }

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
