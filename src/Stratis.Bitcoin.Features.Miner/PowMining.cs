using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    public class ReserveScript
    {
        public ReserveScript()
        {
        }

        public ReserveScript(Script reserveSfullNodecript)
        {
            this.reserveSfullNodecript = reserveSfullNodecript;
        }

        public Script reserveSfullNodecript { get; set; }
    }

    public class PowMining
    {
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

        private const int InnerLoopCount = 0x10000;

        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        private readonly ConsensusLoop consensusLoop;

        private readonly ConcurrentChain chain;

        private readonly Network network;

        private readonly IDateTimeProvider dateTimeProvider;

        private readonly AssemblerFactory blockAssemblerFactory;

        private readonly IBlockRepository blockRepository;

        private readonly ChainState chainState;

        private readonly Signals.Signals signals;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly IAsyncLoopFactory asyncLoopFactory;

        private uint256 hashPrevBlock;

        private IAsyncLoop mining;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public PowMining(
            ConsensusLoop consensusLoop,
            ConcurrentChain chain,
            Network network,
            IDateTimeProvider dateTimeProvider,
            AssemblerFactory blockAssemblerFactory,
            IBlockRepository blockRepository,
            ChainState chainState,
            Signals.Signals signals,
            INodeLifetime nodeLifetime,
            IAsyncLoopFactory asyncLoopFactory,
            ILoggerFactory loggerFactory)
        {
            this.consensusLoop = consensusLoop;
            this.chain = chain;
            this.network = network;
            this.dateTimeProvider = dateTimeProvider;
            this.blockAssemblerFactory = blockAssemblerFactory;
            this.blockRepository = blockRepository;
            this.chainState = chainState;
            this.signals = signals;
            this.nodeLifetime = nodeLifetime;
            this.asyncLoopFactory = asyncLoopFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public IAsyncLoop Mine(Script reserveScript)
        {
            if (this.mining != null)
                return this.mining; // already mining

            this.mining = this.asyncLoopFactory.Run("PowMining.Mine", token =>
            {
                this.GenerateBlocks(new ReserveScript { reserveSfullNodecript = reserveScript }, int.MaxValue, int.MaxValue);
                this.mining = null;
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.RunOnce,
            startAfter: TimeSpans.TenSeconds);

            return this.mining;
        }

        /// <summary>
        /// Generates up to a specified number of blocks with a limited number of attempts.
        /// </summary>
        /// <param name="reserveScript"></param>
        /// <param name="generate">Number of blocks to generate. It is possible that less than the required number of blocks will be mined.</param>
        /// <param name="maxTries">Maximum number of attempts the miner will calculate PoW hash in order to find suitable ones to generate specified amount of blocks.</param>
        /// <returns>List with generated block's hashes</returns>
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
                this.nodeLifetime.ApplicationStopping.ThrowIfCancellationRequested();

                ChainedBlock chainTip = this.consensusLoop.Tip;
                if (this.chain.Tip != chainTip)
                {
                    Task.Delay(TimeSpan.FromMinutes(1), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                    continue;
                }

                BlockTemplate pblockTemplate = this.blockAssemblerFactory.Create(chainTip).CreateNewBlock(reserveScript.reserveSfullNodecript);

                if (this.network.NetworkOptions.IsProofOfStake)
                {
                    // Make sure the POS consensus rules are valid. This is required for generation of blocks inside tests,
                    // where it is possible to generate multiple blocks within one second.
                    if (pblockTemplate.Block.Header.Time <= chainTip.Header.Time)
                    {
                        continue;
                    }
                }

                this.IncrementExtraNonce(pblockTemplate.Block, chainTip, nExtraNonce);
                Block pblock = pblockTemplate.Block;

                while ((maxTries > 0) && (pblock.Header.Nonce < InnerLoopCount) && !pblock.CheckProofOfWork())
                {
                    this.nodeLifetime.ApplicationStopping.ThrowIfCancellationRequested();

                    ++pblock.Header.Nonce;
                    --maxTries;
                }

                if (maxTries == 0)
                    break;

                if (pblock.Header.Nonce == InnerLoopCount)
                    continue;

                var newChain = new ChainedBlock(pblock.Header, pblock.GetHash(), chainTip);

                if (newChain.ChainWork <= chainTip.ChainWork)
                    continue;

                var blockValidationContext = new BlockValidationContext { Block = pblock };

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
                blocks.Add(pblock.GetHash());

                pblockTemplate = null;
            }

            return blocks;
        }

        public void IncrementExtraNonce(Block pblock, ChainedBlock pindexPrev, int nExtraNonce)
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
        }
    }
}
