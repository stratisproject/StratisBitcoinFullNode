﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Mining;
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
        private readonly IConsensusLoop consensusLoop;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Default for "-blockmintxfee", which sets the minimum feerate for a transaction in blocks created by mining code.</summary>
        public const int DefaultBlockMinTxFee = 1000;

        /// <summary>Default for "-blockmaxsize", which controls the maximum size of block the mining code will create.</summary>
        public const int DefaultBlockMaxSize = 750000;

        /// <summary>
        /// Default for "-blockmaxweight", which controls the maximum block weight the mining code can create.
        /// Block is measured in weight units. Data which touches the UTXO (What addresses are involved in the transaction, how many coins are being transferred) costs
        /// 4 weight units (WU) per byte. Witness data (signatures used to unlock existing coins so that they can be spent) costs 1 WU per byte.
        /// <seealso cref="http://learnmeabitcoin.com/faq/segregated-witness"/>
        /// </summary>
        public const int DefaultBlockMaxWeight = 3000000;

        private uint256 hashPrevBlock;

        private const int InnerLoopCount = 0x10000;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

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
            IConsensusLoop consensusLoop,
            ConcurrentChain chain,
            IDateTimeProvider dateTimeProvider,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.blockProvider = blockProvider;
            this.chain = chain;
            this.consensusLoop = consensusLoop;
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.mempool = mempool;
            this.mempoolLock = mempoolLock;
            this.network = network;
            this.nodeLifetime = nodeLifetime;
            this.miningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(new[] { this.nodeLifetime.ApplicationStopping });
        }

        ///<inheritdoc/>
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

        ///<inheritdoc/>
        public void StopMining()
        {
            this.miningCancellationTokenSource.Cancel();
            this.miningLoop?.Dispose();
            this.miningLoop = null;
            this.miningCancellationTokenSource.Dispose();
            this.miningCancellationTokenSource = null;
        }

        ///<inheritdoc/>
        public List<uint256> GenerateBlocks(ReserveScript reserveScript, ulong amountOfBlocksToMine, ulong maxTries)
        {
            var context = new MineBlockContext(amountOfBlocksToMine, (ulong)this.chain.Height, maxTries, reserveScript);

            while (context.MiningCanContinue)
            {
                if (!ConsensusIsAtTip(context))
                    continue;

                if (!BuildBlock(context))
                    continue;

                if (!MineBlock(context))
                    break;

                if (!ValidateMinedBlock(context))
                    continue;

                ValidateMinedBlockWithConsensus(context);

                if (!CheckValidationContext(context))
                    break;

                if (!CheckValidationContextPreviousTip(context))
                    continue;

                OnBlockMined(context);
            }

            return context.Blocks;
        }

        private bool ConsensusIsAtTip(MineBlockContext context)
        {
            this.miningCancellationTokenSource.Token.ThrowIfCancellationRequested();

            context.ChainTip = this.consensusLoop.Tip;
            if (this.chain.Tip != context.ChainTip)
            {
                Task.Delay(TimeSpan.FromMinutes(1), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                return false;
            }

            return true;
        }

        private bool BuildBlock(MineBlockContext context)
        {
            context.BlockTemplate = this.blockProvider.BuildPowBlock(context.ChainTip, context.ReserveScript.ReserveFullNodeScript);

            if (this.network.Consensus.IsProofOfStake)
            {
                // Make sure the POS consensus rules are valid. This is required for generation of blocks inside tests,
                // where it is possible to generate multiple blocks within one second.
                if (context.BlockTemplate.Block.Header.Time <= context.ChainTip.Header.Time)
                    return false;
            }

            return true;
        }

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

        private bool ValidateMinedBlock(MineBlockContext context)
        {
            if (context.BlockTemplate.Block.Header.Nonce == InnerLoopCount)
                return false;

            var chainedHeader = new ChainedHeader(context.BlockTemplate.Block.Header, context.BlockTemplate.Block.GetHash(), context.ChainTip);
            if (chainedHeader.ChainWork <= context.ChainTip.ChainWork)
                return false;

            return true;
        }

        private void ValidateMinedBlockWithConsensus(MineBlockContext context)
        {
            context.ValidationContext = new ValidationContext { Block = context.BlockTemplate.Block };
            this.consensusLoop.AcceptBlockAsync(context.ValidationContext).GetAwaiter().GetResult();
        }

        private bool CheckValidationContext(MineBlockContext context)
        {
            if (context.ValidationContext.ChainedHeader == null)
            {
                this.logger.LogTrace("(-)[REORG-2]");
                return false;
            }

            if (context.ValidationContext.Error != null && context.ValidationContext.Error != ConsensusErrors.InvalidPrevTip)
            {
                this.logger.LogTrace("(-)[ACCEPT_BLOCK_ERROR]");
                return false;
            }

            return true;
        }

        private bool CheckValidationContextPreviousTip(MineBlockContext context)
        {
            if (context.ValidationContext.Error != null)
                if (context.ValidationContext.Error == ConsensusErrors.InvalidPrevTip)
                    return false;
            return true;
        }

        private void OnBlockMined(MineBlockContext context)
        {
            this.logger.LogInformation("Mined new {0} block: '{1}'.", BlockStake.IsProofOfStake(context.ValidationContext.Block) ? "POS" : "POW", context.ValidationContext.ChainedHeader);

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

        private class MineBlockContext
        {
            private readonly ulong amountOfBlocksToMine;
            public List<uint256> Blocks = new List<uint256>();
            public BlockTemplate BlockTemplate { get; set; }
            public ulong ChainHeight { get; set; }
            public ulong CurrentHeight { get; set; }
            public ChainedHeader ChainTip { get; set; }
            public int ExtraNonce { get; set; }
            public ulong MaxTries { get; set; }
            public bool MiningCanContinue { get { return this.CurrentHeight < this.ChainHeight + this.amountOfBlocksToMine; } }
            public readonly ReserveScript ReserveScript;
            public ValidationContext ValidationContext { get; set; }

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