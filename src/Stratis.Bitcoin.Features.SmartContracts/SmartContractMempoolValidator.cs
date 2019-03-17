using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.ContractSigning;
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

        /// <summary>
        /// These rules can be checked instantly. They don't rely on other parts of the context to be loaded.
        /// </summary>
        private readonly List<ISmartContractMempoolRule> preTxRules;

        /// <summary>
        /// These rules rely on the fee part of the context to be loaded in parent class. See 'AcceptToMemoryPoolWorkerAsync'.
        /// </summary>
        private readonly List<ISmartContractMempoolRule> feeTxRules;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly IStateRepositoryRoot stateRepositoryRoot;

        public SmartContractMempoolValidator(ITxMempool memPool, MempoolSchedulerLock mempoolLock,
            IDateTimeProvider dateTimeProvider, MempoolSettings mempoolSettings, ConcurrentChain chain,
            ICoinView coinView, ILoggerFactory loggerFactory, NodeSettings nodeSettings,
            IConsensusRuleEngine consensusRules, ICallDataSerializer callDataSerializer, Network network,
            IStateRepositoryRoot stateRepositoryRoot)
            : base(memPool, mempoolLock, dateTimeProvider, mempoolSettings, chain, coinView, loggerFactory, nodeSettings, consensusRules)
        {
            // Dirty hack, but due to AllowedScriptTypeRule we don't need to check for standard scripts on any network, even live.
            // TODO: Remove ASAP. Ensure RequireStandard isn't used on SC mainnets, or the StandardScripts check is modular.
            mempoolSettings.RequireStandard = false;

            this.callDataSerializer = callDataSerializer;
            this.stateRepositoryRoot = stateRepositoryRoot;

            var p2pkhRule = new P2PKHNotContractRule(stateRepositoryRoot);

            var scriptTypeRule = new AllowedScriptTypeRule(network);
            scriptTypeRule.Initialize();

            this.preTxRules = new List<ISmartContractMempoolRule>
            {
                new MempoolOpSpendRule(),
                new TxOutSmartContractExecRule(),
                scriptTypeRule,
                p2pkhRule
            };

            // TODO: Tidy this up. Rules should be injected? Shouldn't be generating here based on Network.
            var txChecks = new List<IContractTransactionValidationLogic>
            {
                new SmartContractFormatLogic()
            };

            if (network is ISignedCodePubKeyHolder holder)
            {
                txChecks.Add(new ContractSignedCodeLogic(new ContractSigner(), holder.SigningContractPubKey));
            }


            this.feeTxRules = new List<ISmartContractMempoolRule>()
            {
                new ContractTransactionValidationRule(this.callDataSerializer, txChecks)
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

        /// <inheritdoc />
        public override void CheckFee(MempoolValidationContext context)
        {
            base.CheckFee(context);

            foreach (ISmartContractMempoolRule rule in this.feeTxRules)
            {
                rule.CheckTransaction(context);
            }

            this.CheckMinGasLimit(context);
        }

        private void CheckMinGasLimit(MempoolValidationContext context)
        {
            Transaction transaction = context.Transaction;

            if (!transaction.IsSmartContractExecTransaction())
                return;

            // We know it has passed SmartContractFormatRule so we can deserialize it easily.
            TxOut scTxOut = transaction.TryGetSmartContractTxOut();
            Result<ContractTxData> callDataDeserializationResult = this.callDataSerializer.Deserialize(scTxOut.ScriptPubKey.ToBytes());
            ContractTxData callData = callDataDeserializationResult.Value;
            if (callData.GasPrice < MinGasPrice)
                context.State.Fail(MempoolErrors.InsufficientFee, $"Gas price {callData.GasPrice} is below required price: {MinGasPrice}").Throw();
        }
    }
}