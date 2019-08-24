using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.PoS.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.PoS
{
    public sealed class SmartContractPosRuleRegistration : IRuleRegistration
    {
        private readonly Network network;
        private readonly IStateRepositoryRoot stateRepositoryRoot;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly ISenderRetriever senderRetriever;
        private readonly IReceiptRepository receiptRepository;
        private readonly ICoinView coinView;
        private readonly IStakeChain stakeChain;
        private readonly IStakeValidator stakeValidator;

        public SmartContractPosRuleRegistration()
        {
        }

        public void RegisterRules(IServiceCollection services)
        {
            foreach (Type ruleType in new List<Type>()
            {
                typeof(HeaderTimeChecksRule),
                typeof(HeaderTimeChecksPosRule),
                typeof(StratisBugFixPosFutureDriftRule),
                typeof(CheckDifficultyPosRule),
                typeof(StratisHeaderVersionRule),
            })
                services.AddSingleton(typeof(IHeaderValidationConsensusRule), ruleType);

            foreach (Type ruleType in new List<Type>()
            {
                typeof(BlockMerkleRootRule),
                typeof(PosBlockSignatureRepresentationRule),
                typeof(SmartContractPosBlockSignatureRule),
            })
                services.AddSingleton(typeof(IIntegrityValidationConsensusRule), ruleType);

            foreach (Type ruleType in new List<Type>()
            {
                typeof(SetActivationDeploymentsPartialValidationRule),

                typeof(PosTimeMaskRule),

                // rules that are inside the method ContextualCheckBlock
                typeof(TransactionLocktimeActivationRule), // implements BIP113
                typeof(CoinbaseHeightActivationRule), // implements BIP34
                typeof(WitnessCommitmentsRule), // BIP141, BIP144
                typeof(BlockSizeRule),

                // rules that are inside the method CheckBlock
                typeof(EnsureCoinbaseRule),
                typeof(CheckPowTransactionRule),
                typeof(CheckPosTransactionRule),
                typeof(CheckSigOpsRule),
                typeof(PosCoinstakeRule)
            })
                services.AddSingleton(typeof(IPartialValidationConsensusRule), ruleType);

            // TODO: When looking to make PoS work again, will need to add several of the smart contract consensus rules below (see PoA and PoW implementations)
            foreach (Type ruleType in new List<Type>()
            {
                typeof(SetActivationDeploymentsFullValidationRule),

                typeof(CheckDifficultyHybridRule),
                typeof(LoadCoinviewRule),
                typeof(TransactionDuplicationActivationRule), // implements BIP30
                typeof(SmartContractPosCoinviewRule), // implements BIP68, MaxSigOps and BlockReward 
                typeof(SaveCoinviewRule)
            })
                services.AddSingleton(typeof(IFullValidationConsensusRule), ruleType);

        }
    }
}