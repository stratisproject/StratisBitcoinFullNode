using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
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
            ISenderRetriever senderRetriever, IReceiptRepository receiptRepository, ICoinView coinView, IBlockExecutionResultCache executionCache, ILoggerFactory loggerFactory) 
            : base(network, stateRepositoryRoot, executorFactory, callDataSerializer, senderRetriever, receiptRepository, coinView, executionCache, loggerFactory)
        {
        }

        /// <inheritdoc />
        protected override Money GetTransactionFee(UnspentOutputSet view, Transaction tx)
        {
            return view.GetValueIn(tx) - tx.TotalOut;
        }
    }
}