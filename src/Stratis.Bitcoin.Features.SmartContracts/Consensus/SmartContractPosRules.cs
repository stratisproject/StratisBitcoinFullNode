﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    /// <summary>
    /// Extension of consensus rules that provide access to a PoS store.
    /// </summary>
    public sealed class SmartContractPosRules : PosConsensusRules
    {
        public readonly ISmartContractExecutorFactory ExecutorFactory;
        public readonly ContractStateRepositoryRoot OriginalStateRoot;
        public readonly ISmartContractReceiptStorage ReceiptStorage;

        public SmartContractPosRules(
            ConcurrentChain chain,
            ICheckpoints checkpoints,
            ConsensusSettings consensusSettings,
            IDateTimeProvider dateTimeProvider,
            ISmartContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            Network network,
            NodeDeployments nodeDeployments,
            ContractStateRepositoryRoot originalStateRoot,
            ILookaheadBlockPuller puller,
            ISmartContractReceiptStorage receiptStorage,
            IStakeChain stakeChain,
            IStakeValidator stakeValidator,
            ICoinView utxoSet)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, utxoSet, puller, stakeChain, stakeValidator)
        {
            this.ExecutorFactory = executorFactory;
            this.OriginalStateRoot = originalStateRoot;
            this.ReceiptStorage = receiptStorage;
        }

        /// <inheritdoc />
        public override RuleContext CreateRuleContext(ValidationContext validationContext, ChainedHeader consensusTip)
        {
            return new PosRuleContext(validationContext, this.Network.Consensus, consensusTip, this.DateTimeProvider.GetTimeOffset());
        }
    }
}