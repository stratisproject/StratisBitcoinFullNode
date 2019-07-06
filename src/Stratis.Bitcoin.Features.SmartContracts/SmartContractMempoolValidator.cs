using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    /// <summary>
    /// Provides the same functionality as the original mempool validator with some extra validation.
    /// </summary>
    public class SmartContractMempoolValidator : MempoolValidator
    {
        /// <summary>The "functional" minimum gas limit. Not enforced by consensus but miners are only going to pick transactions up if their gas price is higher than this.</summary>
        public const ulong MinGasPrice = 100;

        private readonly ICallDataSerializer callDataSerializer;
        private readonly IStateRepositoryRoot stateRepositoryRoot;

        public SmartContractMempoolValidator(ITxMempool memPool, MempoolSchedulerLock mempoolLock,
            IDateTimeProvider dateTimeProvider, MempoolSettings mempoolSettings, ChainIndexer chainIndexer,
            ICoinView coinView, ILoggerFactory loggerFactory, NodeSettings nodeSettings,
            IConsensusRuleEngine consensusRules, ICallDataSerializer callDataSerializer, Network network,
            IStateRepositoryRoot stateRepositoryRoot,
            IEnumerable<IContractTransactionFullValidationRule> txFullValidationRules)
            : base(memPool, mempoolLock, dateTimeProvider, mempoolSettings, chainIndexer, coinView, loggerFactory, nodeSettings, consensusRules)
        {
            // Dirty hack, but due to AllowedScriptTypeRule we don't need to check for standard scripts on any network, even live.
            // TODO: Remove ASAP. Ensure RequireStandard isn't used on SC mainnets, or the StandardScripts check is modular.
            mempoolSettings.RequireStandard = false;

            this.callDataSerializer = callDataSerializer;
            this.stateRepositoryRoot = stateRepositoryRoot;

            var p2pkhRule = new P2PKHNotContractRule(stateRepositoryRoot);

            var scriptTypeRule = new AllowedScriptTypeRule(network);
            scriptTypeRule.Initialize();

            var txChecks = new List<IContractTransactionPartialValidationRule>()
            {
                new SmartContractFormatLogic()
            };
            
            this.mempoolRules = new List<IMempoolRule>
            {
                new MempoolOpSpendRule(),
                new TxOutSmartContractExecRule(),
                scriptTypeRule,
                p2pkhRule,

                // The non-SC mempool rules
                new CheckConflictsMempoolRule(),
                new CheckCoinViewMempoolRule(),
                new CreateMempoolEntryMempoolRule(),
                new CheckSigOpsMempoolRule(),
                new CheckFeeMempoolRule(),

                // The smart contract mempool needs to do more fee checks than its counterpart, so include extra rules
                new ContractTransactionPartialValidationRule(this.callDataSerializer, txChecks),
                new ContractTransactionFullValidationRule(this.callDataSerializer, txFullValidationRules),
                new CheckMinGasLimitSmartContractMempoolRule(),

                // Remaining non-SC rules
                new CheckRateLimitMempoolRule(),
                new CheckAncestorsMempoolRule(),
                new CheckReplacementMempoolRule(),
                new CheckAllInputsMempoolRule()
            };
        }

        /// <inheritdoc />
        protected override void PreMempoolChecks(MempoolValidationContext context)
        {
            base.PreMempoolChecks(context);

            foreach (ISmartContractMempoolRule rule in this.preTxRules)
            {
                rule.CheckTransaction(context);
            }
        }
    }
}