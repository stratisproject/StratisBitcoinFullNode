﻿using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Executor.Reflection;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    /// <summary>
    /// Provides the same functionality as the original mempool validator with some extra validation.
    /// </summary>
    public class SmartContractMempoolValidator : MempoolValidator
    {
        /// <summary>
        /// These rules can be checked instantly. They don't rely on other parts of the context to be loaded.
        /// </summary>
        private readonly List<ISmartContractMempoolRule> preTxRules;

        /// <summary>
        /// These rules rely on the fee part of the context to be loaded in parent class. See 'AcceptToMemoryPoolWorkerAsync'.
        /// </summary>
        private readonly List<ISmartContractMempoolRule> feeTxRules;
        private readonly ICallDataSerializer callDataSerializer;

        public SmartContractMempoolValidator(ITxMempool memPool, MempoolSchedulerLock mempoolLock, IDateTimeProvider dateTimeProvider, MempoolSettings mempoolSettings, ConcurrentChain chain, ICoinView coinView, ILoggerFactory loggerFactory, NodeSettings nodeSettings, IConsensusRuleEngine consensusRules, ICallDataSerializer callDataSerializer)
            : base(memPool, mempoolLock, dateTimeProvider, mempoolSettings, chain, coinView, loggerFactory, nodeSettings, consensusRules)
        {
            var p2pkhRule = new P2PKHNotContractRule();
            p2pkhRule.Parent = (ConsensusRuleEngine) consensusRules;
            p2pkhRule.Initialize();

            var scriptTypeRule = new AllowedScriptTypeRule();
            scriptTypeRule.Parent = (ConsensusRuleEngine) consensusRules;
            scriptTypeRule.Initialize();

            this.preTxRules = new List<ISmartContractMempoolRule>
            {
                new MempoolOpSpendRule(),
                new TxOutSmartContractExecRule(),
                scriptTypeRule,
                p2pkhRule
            };

            this.feeTxRules = new List<ISmartContractMempoolRule>()
            {
                new SmartContractFormatRule(callDataSerializer)
            };
        }

        /// <inheritdoc />
        protected override void PreMempoolChecks(MempoolValidationContext context)
        {
            base.PreMempoolChecks(context);

            foreach (ISmartContractMempoolRule rule in preTxRules)
            {
                rule.CheckTransaction(context);
            }
        }

        /// <inheritdoc />
        public override void CheckFee(MempoolValidationContext context)
        {
            base.CheckFee(context);

            foreach (ISmartContractMempoolRule rule in feeTxRules)
            {
                rule.CheckTransaction(context);
            }
        }
    }
}