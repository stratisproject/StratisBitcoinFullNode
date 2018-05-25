﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    public class PosBlockAssembler : BlockAssembler
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Database of stake related data for the current blockchain.</summary>
        private readonly IStakeChain stakeChain;

        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        private readonly IStakeValidator stakeValidator;

        public PosBlockAssembler(
            IConsensusLoop consensusLoop,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            IStakeChain stakeChain,
            IStakeValidator stakeValidator)
            : base(consensusLoop, dateTimeProvider, loggerFactory, mempool, mempoolLock, network, new AssemblerOptions() { IsProofOfStake = true })
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stakeChain = stakeChain;
            this.stakeValidator = stakeValidator;
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(chainTip), chainTip, nameof(scriptPubKey), nameof(scriptPubKey.Length), scriptPubKey.Length);

            this.OnBuild(chainTip, scriptPubKey);

            this.coinbase.Outputs[0].ScriptPubKey = new Script();
            this.coinbase.Outputs[0].Value = Money.Zero;

            this.logger.LogTrace("(-)");

            return this.BlockTemplate;
        }

        public override void OnUpdateHeaders()
        {
            this.logger.LogTrace("()");

            this.block.Header.HashPrevBlock = this.ChainTip.HashBlock;
            this.block.Header.UpdateTime(this.DateTimeProvider.GetTimeOffset(), this.Network, this.ChainTip);
            this.block.Header.Nonce = 0;
            this.block.Header.Bits = this.stakeValidator.GetNextTargetRequired(this.stakeChain, this.ChainTip, this.Network.Consensus, this.Options.IsProofOfStake);

            this.logger.LogTrace("(-)");
        }

        public override void OnTestBlockValidity()
        {
            this.logger.LogTrace("()");

            this.logger.LogTrace("(-)");
        }
    }
}