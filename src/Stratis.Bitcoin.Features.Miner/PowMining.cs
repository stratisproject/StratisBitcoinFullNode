using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner.Interfaces;
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
        private readonly PowBlockAssembler blockBuilder;

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
            PowBlockAssembler blockBuilder,
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
            this.blockBuilder = blockBuilder;
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
        public List<uint256> GenerateBlocks(ReserveScript reserveScript, ulong generate, ulong maxTries)
        {
            ulong nHeightStart = 0;
            ulong nHeightEnd = 0;
            ulong nHeight = 0;

            nHeightStart = (ulong)this.chain.Height;
            nHeight = nHeightStart;
            nHeightEnd = nHeightStart + generate;
            int nExtraNonce = 0;
            var blocks = new List<uint256>();

            while (nHeight < nHeightEnd)
            {
                this.miningCancellationTokenSource.Token.ThrowIfCancellationRequested();

                ChainedBlock chainTip = this.consensusLoop.Tip;
                if (this.chain.Tip != chainTip)
                {
                    Task.Delay(TimeSpan.FromMinutes(1), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                    continue;
                }

                BlockTemplate blockTemplate = this.blockBuilder.Build(chainTip, reserveScript.ReserveFullNodeScript);

                if (this.network.Consensus.IsProofOfStake)
                {
                    // Make sure the POS consensus rules are valid. This is required for generation of blocks inside tests,
                    // where it is possible to generate multiple blocks within one second.
                    if (blockTemplate.Block.Header.Time <= chainTip.Header.Time)
                        continue;
                }

                nExtraNonce = this.IncrementExtraNonce(blockTemplate.Block, chainTip, nExtraNonce);
                Block block = blockTemplate.Block;

                while ((maxTries > 0) && (block.Header.Nonce < InnerLoopCount) && !block.CheckProofOfWork(this.network.Consensus))
                {
                    this.miningCancellationTokenSource.Token.ThrowIfCancellationRequested();

                    ++block.Header.Nonce;
                    --maxTries;
                }

                if (maxTries == 0)
                    break;

                if (block.Header.Nonce == InnerLoopCount)
                    continue;

                var newChain = new ChainedBlock(block.Header, block.GetHash(), chainTip);

                if (newChain.ChainWork <= chainTip.ChainWork)
                    continue;

                var blockValidationContext = new BlockValidationContext { Block = block };

                this.consensusLoop.AcceptBlockAsync(blockValidationContext).GetAwaiter().GetResult();

                if (blockValidationContext.ChainedBlock == null)
                {
                    this.logger.LogTrace("(-)[REORG-2]");
                    return blocks;
                }

                if (blockValidationContext.Error != null)
                {
                    if (blockValidationContext.Error == ConsensusErrors.InvalidPrevTip)
                        continue;

                    this.logger.LogTrace("(-)[ACCEPT_BLOCK_ERROR]");
                    return blocks;
                }

                this.logger.LogInformation("Mined new {0} block: '{1}'.", BlockStake.IsProofOfStake(blockValidationContext.Block) ? "POS" : "POW", blockValidationContext.ChainedBlock);

                nHeight++;
                blocks.Add(block.GetHash());

                blockTemplate = null;
            }

            return blocks;
        }

        ///<inheritdoc/>
        public int IncrementExtraNonce(Block pblock, ChainedBlock pindexPrev, int nExtraNonce)
        {
            // Update nExtraNonce
            if (this.hashPrevBlock != pblock.Header.HashPrevBlock)
            {
                nExtraNonce = 0;
                this.hashPrevBlock = pblock.Header.HashPrevBlock;
            }

            nExtraNonce++;
            int nHeight = pindexPrev.Height + 1; // Height first in coinbase required for block.version=2
            Transaction txCoinbase = pblock.Transactions[0];
            txCoinbase.Inputs[0] = TxIn.CreateCoinbase(nHeight);

            Guard.Assert(txCoinbase.Inputs[0].ScriptSig.Length <= 100);
            pblock.UpdateMerkleRoot();

            return nExtraNonce;
        }
    }
}