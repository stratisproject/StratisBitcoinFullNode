using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.PoW.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Bitcoin.Features.SmartContracts.PoW
{
    public sealed class SmartContractPowRuleRegistration : IRuleRegistration
    {
        private readonly Network network;
        private readonly IStateRepositoryRoot stateRepositoryRoot;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly ISenderRetriever senderRetriever;
        private readonly IReceiptRepository receiptRepository;
        private readonly ICoinView coinView;

        public SmartContractPowRuleRegistration()
        {
        }

        public void RegisterRules(IServiceCollection services)
        {
            foreach (Type ruleType in new List<Type>()
            {
                typeof(HeaderTimeChecksRule),
                typeof(CheckDifficultyPowRule),
                typeof(BitcoinActivationRule),
                typeof(BitcoinHeaderVersionRule)
            })
                services.AddSingleton(typeof(IHeaderValidationConsensusRule), ruleType);

            foreach (Type ruleType in new List<Type>()
            {
                typeof(BlockMerkleRootRule)
            })
                services.AddSingleton(typeof(IIntegrityValidationConsensusRule), ruleType);

            foreach (Type ruleType in new List<Type>()
            {
                typeof(SetActivationDeploymentsPartialValidationRule),

                typeof(TransactionLocktimeActivationRule), // implements BIP113
                typeof(CoinbaseHeightActivationRule), // implements BIP34
                typeof(WitnessCommitmentsRule), // BIP141, BIP144
                typeof(BlockSizeRule),

                // rules that are inside the method CheckBlock
                typeof(EnsureCoinbaseRule),
                typeof(CheckPowTransactionRule),
                typeof(CheckSigOpsRule),
                typeof(AllowedScriptTypeRule),
                typeof(ContractTransactionPartialValidationRule)
            })
                services.AddSingleton(typeof(IPartialValidationConsensusRule), ruleType);

            services.AddSingleton(typeof(IContractTransactionPartialValidationRule), typeof(SmartContractFormatLogic));

            foreach (Type ruleType in new List<Type>()
            {
                typeof(SetActivationDeploymentsFullValidationRule),

                typeof(LoadCoinviewRule),
                typeof(TransactionDuplicationActivationRule), // implements BIP30
                typeof(TxOutSmartContractExecRule),
                typeof(OpSpendRule),
                typeof(CanGetSenderRule),
                typeof(P2PKHNotContractRule),
                typeof(SmartContractPowCoinviewRule), // implements BIP68, MaxSigOps and BlockReward 
                typeof(SaveCoinviewRule)
            })
                services.AddSingleton(typeof(IFullValidationConsensusRule), ruleType);

        }
    }
}