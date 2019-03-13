//using Microsoft.Extensions.Logging;
//using NBitcoin;
//using Stratis.Bitcoin.Consensus;
//using Stratis.Bitcoin.Consensus.Rules;
//using Stratis.Bitcoin.Features.Consensus.CoinViews;
//using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
//using Stratis.SmartContracts.CLR;
//using Stratis.SmartContracts.Core;
//using Stratis.SmartContracts.Core.Receipts;
//using Stratis.SmartContracts.Core.State;
//using Stratis.SmartContracts.Core.Util;

//namespace Stratis.Features.FederatedPeg
//{
//    public class FedPegPoACoinviewRule : SmartContractPoACoinviewRule
//    {
//        public FedPegPoACoinviewRule(IStateRepositoryRoot stateRepositoryRoot, IContractExecutorFactory executorFactory, ICallDataSerializer callDataSerializer, ISenderRetriever senderRetriever, IReceiptRepository receiptRepository, ICoinView coinView) : base(stateRepositoryRoot, executorFactory, callDataSerializer, senderRetriever, receiptRepository, coinView)
//        {
//        }

//        /// <summary>
//        /// Ensures that our block reward is given in several outputs for the premine.
//        /// </summary>
//        public override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
//        {
//            if (height == this.network.Consensus.PremineHeight)
//            {

//            }
//            else
//            {
//                if (block.Transactions[0].TotalOut > fees)
//                {
//                    this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
//                    ConsensusErrors.BadCoinbaseAmount.Throw();
//                }
//            }
//        }
//    }
//}
