﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.Bitcoin.Features.SmartContracts.PoW;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    /// <summary>
    /// Pushes everything to the <see cref="SmartContractBlockDefinition"/>, just amends the block difficulty for PoA.
    /// </summary>
    public class SmartContractPoABlockDefinition : SmartContractBlockDefinition
    {
        public SmartContractPoABlockDefinition(
            IBlockBufferGenerator blockBufferGenerator,
            ICoinView coinView,
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            IContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            ISenderRetriever senderRetriever,
            IStateRepositoryRoot stateRoot,
            IBlockExecutionResultCache executionCache,
            ICallDataSerializer callDataSerializer,
            MinerSettings minerSettings)
            : base(blockBufferGenerator, coinView, consensusManager, dateTimeProvider, executorFactory, loggerFactory, mempool,
                mempoolLock, minerSettings, network, senderRetriever, stateRoot, executionCache, callDataSerializer)
        {
            // TODO: Fix gross MinerSettings injection ^^
        }

        /// <inheritdoc/>
        public override void UpdateHeaders()
        {
            base.UpdateHeaders();

            this.block.Header.Bits = PoAHeaderDifficultyRule.PoABlockDifficulty;
        }
    }
}
