using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.PoW.Rules
{
    public sealed class SmartContractPowCoinviewRule : SmartContractCoinviewRule
    {
        public SmartContractPowCoinviewRule(Network network, IStateRepositoryRoot stateRepositoryRoot,
            IContractExecutorFactory executorFactory, ICallDataSerializer callDataSerializer,
            ISenderRetriever senderRetriever, IReceiptRepository receiptRepository, ICoinView coinView) 
            : base(network, stateRepositoryRoot, executorFactory, callDataSerializer, senderRetriever, receiptRepository, coinView)
        {
        }
    }
}