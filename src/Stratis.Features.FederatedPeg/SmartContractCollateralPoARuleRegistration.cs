using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Features.FederatedPeg
{
    public class SmartContractCollateralPoARuleRegistration : SmartContractPoARuleRegistration
    {
        private readonly IInitialBlockDownloadState ibdState;

        private readonly ISlotsManager slotsManager;

        private readonly ICollateralChecker collateralChecker;

        private readonly IDateTimeProvider dateTime;

        public SmartContractCollateralPoARuleRegistration(Network network, IStateRepositoryRoot stateRepositoryRoot, IContractExecutorFactory executorFactory,
            ICallDataSerializer callDataSerializer, ISenderRetriever senderRetriever, IReceiptRepository receiptRepository, ICoinView coinView,
            IEnumerable<IContractTransactionPartialValidationRule> partialTxValidationRules, IEnumerable<IContractTransactionFullValidationRule> fullTxValidationRules,
            IInitialBlockDownloadState ibdState, ISlotsManager slotsManager, ICollateralChecker collateralChecker, IDateTimeProvider dateTime)
        : base(network, stateRepositoryRoot, executorFactory, callDataSerializer, senderRetriever, receiptRepository, coinView, partialTxValidationRules, fullTxValidationRules)
        {
            this.ibdState = ibdState;
            this.slotsManager = slotsManager;
            this.collateralChecker = collateralChecker;
            this.dateTime = dateTime;
        }

        public override void RegisterRules(IConsensus consensus)
        {
            base.RegisterRules(consensus);

            consensus.FullValidationRules.Add(new CheckCollateralFullValidationRule(this.ibdState, this.collateralChecker, this.slotsManager, this.dateTime));
        }
    }
}
