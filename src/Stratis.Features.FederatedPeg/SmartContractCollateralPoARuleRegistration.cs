using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Features.FederatedPeg
{
    public class SmartContractCollateralPoARuleRegistration : SmartContractPoARuleRegistration
    {
        public SmartContractCollateralPoARuleRegistration(Network network, IStateRepositoryRoot stateRepositoryRoot, IContractExecutorFactory executorFactory,
            ICallDataSerializer callDataSerializer, ISenderRetriever senderRetriever, IReceiptRepository receiptRepository, ICoinView coinView,
            IEnumerable<IContractTransactionPartialValidationRule> partialTxValidationRules, IEnumerable<IContractTransactionFullValidationRule> fullTxValidationRules)
        : base(network, stateRepositoryRoot, executorFactory, callDataSerializer, senderRetriever, receiptRepository, coinView, partialTxValidationRules, fullTxValidationRules)
        {
        }

        public override void RegisterRules(IConsensus consensus)
        {
            base.RegisterRules(consensus);

            //consensus.FullValidationRules.Add(); // TODO add colalteral rule
        }
    }
}
