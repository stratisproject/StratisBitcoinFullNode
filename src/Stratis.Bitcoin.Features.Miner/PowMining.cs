using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    public class ReserveScript
    {
        public ReserveScript()
        {
        }

        public ReserveScript(Script reserveFullNodeScript)
        {
            this.ReserveFullNodeScript = reserveFullNodeScript;
        }

        public Script ReserveFullNodeScript { get; set; }
    }

    public class PowMining : IPowMining
    {
        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Builder that creates a proof-of-work block template.</summary>
        private readonly IBlockProvider blockProvider;

        /// <summary>Thread safe chain of block headers from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        private readonly IConsensusManager consensusManager;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Default for "-blockmintxfee", which sets the minimum feerate for a transaction in blocks created by mining code.</summary>
        public const int DefaultBlockMinTxFee = 1000;

        private uint256 hashPrevBlock;

        private const int InnerLoopCount = 0x10000;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <summary>Transaction memory pool for managing transactions in the memory pool.</summary>
        private readonly ITxMempool mempool;

        /// <summary>A lock for managing asynchronous access to memory pool.</summary>
        private readonly MempoolSchedulerLock mempoolLock;

        /// <summary>The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop miningLoop;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>
        /// A cancellation token source that can cancel the mining processes and is linked to the <see cref="INodeLifetime.ApplicationStopping"/>.
        /// </summary>
        private CancellationTokenSource miningCancellationTokenSource;

        public PowMining(
            IAsyncLoopFactory asyncLoopFactory,
            IBlockProvider blockProvider,
            IConsensusManager consensusManager,
            ConcurrentChain chain,
            IDateTimeProvider dateTimeProvider,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState initialBlockDownloadState)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.blockProvider = blockProvider;
            this.chain = chain;
            this.consensusManager = consensusManager;
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.mempool = mempool;
            this.mempoolLock = mempoolLock;
            this.network = network;
            this.nodeLifetime = nodeLifetime;
            this.miningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(new[] { this.nodeLifetime.ApplicationStopping });
        }

        /// <inheritdoc/>
        public void Mine(Script reserveScript)
        {
            if (this.miningLoop != null)
                return;

            this.miningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(new[] { this.nodeLifetime.ApplicationStopping });

            this.miningLoop = this.asyncLoopFactory.Run("PowMining.Mine", token =>
            {
                try
                {
                    this.GenerateBlocks(new ReserveScript { ReserveFullNodeScript = reserveScript }, int.MaxValue, int.MaxValue);
                }
                catch (OperationCanceledException)
                {
                    // Application stopping, nothing to do as the loop will be stopped.
                }
                catch (MinerException me)
                {
                    // Block not accepted by peers or invalid. Should not halt mining.
                    this.logger.LogDebug("Miner exception occurred in miner loop: {0}", me.ToString());
                }
                catch (ConsensusErrorException cee)
                {
                    // Issues constructing block or verifying it. Should not halt mining.
                    this.logger.LogDebug("Consensus error exception occurred in miner loop: {0}", cee.ToString());
                }
                catch
                {
                    this.logger.LogTrace("(-)[UNHANDLED_EXCEPTION]");
                    throw;
                }

                return Task.CompletedTask;
            },
            this.miningCancellationTokenSource.Token,
            repeatEvery: TimeSpans.Second,
            startAfter: TimeSpans.TenSeconds);
        }

        /// <inheritdoc/>
        public void StopMining()
        {
            this.miningCancellationTokenSource.Cancel();
            this.miningLoop?.Dispose();
            this.miningLoop = null;
            this.miningCancellationTokenSource.Dispose();
            this.miningCancellationTokenSource = null;
        }

        /// <inheritdoc/>
        public List<uint256> GenerateBlocks(ReserveScript reserveScript, ulong amountOfBlocksToMine, ulong maxTries)
        {
            var context = new MineBlockContext(amountOfBlocksToMine, (ulong)this.chain.Height, maxTries, reserveScript);

            while (context.MiningCanContinue)
            {
                if (!this.ConsensusIsAtTip(context))
                    continue;

                if (!this.BuildBlock(context))
                    continue;

                if (!this.MineBlock(context))
                    break;

                if (!this.ValidateMinedBlock(context))
                    continue;

                if (!this.ValidateAndConnectBlock(context))
                    continue;

                this.OnBlockMined(context);
            }

            return context.Blocks;
        }

        /// <summary>
        /// Ensures that the node is synced before mining is allowed to start.
        /// </summary>
        private bool ConsensusIsAtTip(MineBlockContext context)
        {
            this.miningCancellationTokenSource.Token.ThrowIfCancellationRequested();

            context.ChainTip = this.consensusManager.Tip;

            // Genesis on a regtest network is a special case. We need to regard ourselves as outside of IBD to
            // bootstrap the mining.
            if (context.ChainTip.Height == 0)
                return true;

            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                Task.Delay(TimeSpan.FromMinutes(1), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a proof of work or proof of stake block depending on the network the node is running on.
        /// <para>
        /// If the node is on a POS network, make sure the POS consensus rules are valid. This is required for
        /// generation of blocks inside tests, where it is possible to generate multiple blocks within one second.
        /// </para>
        /// </summary>
        private bool BuildBlock(MineBlockContext context)
        {
            context.BlockTemplate = this.blockProvider.BuildPowBlock(context.ChainTip, context.ReserveScript.ReserveFullNodeScript);

            if (this.network.Consensus.IsProofOfStake)
            {
                if (context.BlockTemplate.Block.Header.Time <= context.ChainTip.Header.Time)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Executes until the required work (difficulty) has been reached. This is the "mining" process.
        /// </summary>
        private bool MineBlock(MineBlockContext context)
        {
            context.ExtraNonce = this.IncrementExtraNonce(context.BlockTemplate.Block, context.ChainTip, context.ExtraNonce);

            Block block = context.BlockTemplate.Block;
            while ((context.MaxTries > 0) && (block.Header.Nonce < InnerLoopCount) && !block.CheckProofOfWork())
            {
                this.miningCancellationTokenSource.Token.ThrowIfCancellationRequested();

                ++block.Header.Nonce;
                --context.MaxTries;
            }

            if (context.MaxTries == 0)
                return false;

            return true;
        }

        /// <summary>
        /// Ensures that the block was properly mined by checking the block's work against the next difficulty target.
        /// </summary>
        private bool ValidateMinedBlock(MineBlockContext context)
        {
            if (context.BlockTemplate.Block.Header.Nonce == InnerLoopCount)
                return false;

            var chainedHeader = new ChainedHeader(context.BlockTemplate.Block.Header, context.BlockTemplate.Block.GetHash(), context.ChainTip);
            if (chainedHeader.ChainWork <= context.ChainTip.ChainWork)
                return false;

            return true;
        }

        /// <summary>
        /// Validate the mined block by passing it to the consensus rule engine.
        /// <para>
        /// On successful block validation the block will be connected to the chain.
        /// </para>
        /// </summary>
        private bool ValidateAndConnectBlock(MineBlockContext context)
        {
            ChainedHeader chainedHeader = this.consensusManager.BlockMinedAsync(context.BlockTemplate.Block).GetAwaiter().GetResult();

            if (chainedHeader == null)
            {
                this.logger.LogTrace("(-)[BLOCK_VALIDATION_ERROR]:false");
                return false;
            }

            context.ChainedHeaderBlock = new ChainedHeaderBlock(context.BlockTemplate.Block, chainedHeader);

            return true;
        }

        private void OnBlockMined(MineBlockContext context)
        {
            this.logger.LogInformation("Mined new {0} block: '{1}'.", BlockStake.IsProofOfStake(context.ChainedHeaderBlock.Block) ? "POS" : "POW", context.ChainedHeaderBlock.ChainedHeader);

            context.CurrentHeight++;

            context.Blocks.Add(context.BlockTemplate.Block.GetHash());
            context.BlockTemplate = null;
        }

        //<inheritdoc/>
        public int IncrementExtraNonce(Block block, ChainedHeader previousHeader, int extraNonce)
        {
            if (this.hashPrevBlock != block.Header.HashPrevBlock)
            {
                extraNonce = 0;
                this.hashPrevBlock = block.Header.HashPrevBlock;
            }

            extraNonce++;
            int height = previousHeader.Height + 1; // Height first in coinbase required for block.version=2
            Transaction txCoinbase = block.Transactions[0];
            txCoinbase.Inputs[0] = TxIn.CreateCoinbase(height);

            Guard.Assert(txCoinbase.Inputs[0].ScriptSig.Length <= 100);
            block.UpdateMerkleRoot();

            return extraNonce;
        }

        /// <summary>
        /// Context class that holds information on the current state of the mining process (per block).
        /// </summary>
        private class MineBlockContext
        {
            private readonly ulong amountOfBlocksToMine;
            public List<uint256> Blocks = new List<uint256>();
            public BlockTemplate BlockTemplate { get; set; }
            public ulong ChainHeight { get; set; }
            public ChainedHeaderBlock ChainedHeaderBlock { get; internal set; }
            public ulong CurrentHeight { get; set; }
            public ChainedHeader ChainTip { get; set; }
            public int ExtraNonce { get; set; }
            public ulong MaxTries { get; set; }
            public bool MiningCanContinue { get { return this.CurrentHeight < this.ChainHeight + this.amountOfBlocksToMine; } }
            public readonly ReserveScript ReserveScript;

            public MineBlockContext(ulong amountOfBlocksToMine, ulong chainHeight, ulong maxTries, ReserveScript reserveScript)
            {
                this.amountOfBlocksToMine = amountOfBlocksToMine;
                this.ChainHeight = chainHeight;
                this.CurrentHeight = chainHeight;
                this.MaxTries = maxTries;
                this.ReserveScript = reserveScript;
            }
        }
    }
}