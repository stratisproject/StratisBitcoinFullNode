using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;

namespace Stratis.SmartContracts.Core.Util
{
    public interface ISenderRetriever
    {
        /// <summary>
        /// Get the address from a P2PK or a P2PKH.
        /// </summary>
        GetSenderResult GetAddressFromScript(Script script);

        /// <summary>
        /// Get the 'sender' of a transaction.
        /// </summary>
        GetSenderResult GetSender(Transaction tx, ICoinView coinView, IList<Transaction> blockTxs);
    }
}
