using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;

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

        /// <summary>
        /// Get the 'sender' of a transaction in the mempool. Necessary because the MempoolCoinView has a very peculiar API.
        /// </summary>
        GetSenderResult GetSender(Transaction tx, MempoolCoinView coinView);
    }
}
