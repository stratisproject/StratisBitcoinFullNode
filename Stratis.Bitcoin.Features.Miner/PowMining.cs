using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IBlockRepository = Stratis.Bitcoin.Features.BlockStore.IBlockRepository;

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
        // Default for -blockmintxfee, which sets the minimum feerate for a transaction in blocks created by mining code.
        public const int DefaultBlockMinTxFee = 1000;

        // Default for -blockmaxsize, which controls the maximum size of block the mining code will create.
        public const int DefaultBlockMaxSize = 750000;

        // Default for -blockmaxweight, which controls the range of block weights the mining code will create.
        public const int DefaultBlockMaxWeight = 3000000;

        const int InnerLoopCount = 0x10000;

        private readonly ConsensusLoop consensusLoop;
        private readonly ConcurrentChain chain;
        private readonly Network network;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly AssemblerFactory blockAssemblerFactory;
        private readonly IBlockRepository blockRepository;
        private readonly ChainState chainState;
        private readonly Signals.Signals signals;
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

                try
                {
                    if (this.chain.Tip != this.consensusLoop.Tip)
                    {
                        Task.Delay(TimeSpan.FromMinutes(1), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                        continue;
                    }

                    BlockTemplate pblockTemplate = this.blockAssemblerFactory.Create().CreateNewBlock(reserveScript.reserveSfullNodecript);

                    this.IncrementExtraNonce(pblockTemplate.Block, this.chain.Tip, nExtraNonce);
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

                    var newChain = new ChainedBlock(pblock.Header, pblock.GetHash(), this.chain.Tip);

                    if (newChain.ChainWork <= this.chain.Tip.ChainWork)
                        continue;

                    this.chain.SetTip(newChain);

                    var blockResult = new BlockResult { Block = pblock };
                    this.consensusLoop.AcceptBlock(new ContextInformation(blockResult, this.network.Consensus));
                    this.consensusLoop.Puller.SetLocation(newChain);

                    if (blockResult.ChainedBlock == null)
                        break; // Reorg.

                    if (blockResult.Error != null)
                        return blocks;

                    // Push the block to disk, so it is available when peers ask for it.
                    this.blockRepository.PutAsync(blockResult.ChainedBlock.HashBlock, new List<Block> { pblock }).GetAwaiter().GetResult();

                    // Similar logic to what's in the full node code.
                    this.chainState.HighestValidatedPoW = this.consensusLoop.Tip;
                    this.signals.SignalBlock(pblock);

                    this.logger.LogInformation("Mined new {{0}} block: '{1}'.", BlockStake.IsProofOfStake(blockResult.Block) ? "POS" : "POW", blockResult.ChainedBlock.HashBlock);

                    nHeight++;
                    blocks.Add(pblock.GetHash());

                    pblockTemplate = null;
                }
                catch (ConsensusErrorException cer)
                {
                    if (cer.ConsensusError == ConsensusErrors.InvalidPrevTip)
                        continue;

                    throw;
                }
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
