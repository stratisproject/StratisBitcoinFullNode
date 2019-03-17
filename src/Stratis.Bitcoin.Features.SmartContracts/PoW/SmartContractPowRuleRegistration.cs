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

        public SmartContractPowRuleRegistration(Network network,
            IStateRepositoryRoot stateRepositoryRoot,
            IContractExecutorFactory executorFactory,
            ICallDataSerializer callDataSerializer,
            ISenderRetriever senderRetriever,
            IReceiptRepository receiptRepository,
            ICoinView coinView)
        {
            this.network = network;
            this.stateRepositoryRoot = stateRepositoryRoot;
            this.executorFactory = executorFactory;
            this.callDataSerializer = callDataSerializer;
            this.senderRetriever = senderRetriever;
            this.receiptRepository = receiptRepository;
            this.coinView = coinView;
        }

        public void RegisterRules(IConsensus consensus)
        {
            consensus.HeaderValidationRules = new List<IHeaderValidationConsensusRule>()
            {
                new HeaderTimeChecksRule(),
                new CheckDifficultyPowRule(),
                new BitcoinActivationRule(),
                new BitcoinHeaderVersionRule()
            };

            consensus.IntegrityValidationRules = new List<IIntegrityValidationConsensusRule>()
            {
                new BlockMerkleRootRule()
            };

            consensus.PartialValidationRules = new List<IPartialValidationConsensusRule>()
            {
                new SetActivationDeploymentsPartialValidationRule(),

                new TransactionLocktimeActivationRule(), // implements BIP113
                new CoinbaseHeightActivationRule(), // implements BIP34
                new WitnessCommitmentsRule(), // BIP141, BIP144
                new BlockSizeRule(),

                // rules that are inside the method CheckBlock
                new EnsureCoinbaseRule(),
                new CheckPowTransactionRule(),
                new CheckSigOpsRule(),
                new AllowedScriptTypeRule(this.network),
                new ContractTransactionValidationRule(this.callDataSerializer, new List<IContractTransactionValidationLogic>
                {
                    new SmartContractFormatLogic()
                })
            };

            consensus.FullValidationRules = new List<IFullValidationConsensusRule>()
            {
                new SetActivationDeploymentsFullValidationRule(),

                new LoadCoinviewRule(),
                new TransactionDuplicationActivationRule(), // implements BIP30
                new TxOutSmartContractExecRule(),
                new OpSpendRule(),
                new CanGetSenderRule(this.senderRetriever),
                new P2PKHNotContractRule(this.stateRepositoryRoot),
                new SmartContractPowCoinviewRule(this.network, this.stateRepositoryRoot, this.executorFactory, this.callDataSerializer, this.senderRetriever, this.receiptRepository, this.coinView), // implements BIP68, MaxSigOps and BlockReward 
                new SaveCoinviewRule()
            };
        }
    }
}