using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;

namespace Stratis.SmartContracts.Util
{
    /// <summary>
    /// To run smart contracts we need to have a 'From' address. This isn't natively included in a bitcoin transaction
    /// so to get it we check the script of the PrevOut to get an address we can use as 'From'
    /// </summary>
    public static class GetSenderUtil
    {
        public static uint160 GetSender(Transaction tx, CoinView coinView, IList<Transaction> blockTxs)
        {
            Script script = null;
            bool scriptFilled = false;

            if (blockTxs != null & blockTxs.Count > 0)
            {
                foreach (Transaction btx in blockTxs)
                {
                    if (btx.GetHash() == tx.Inputs[0].PrevOut.Hash)
                    {
                        script = btx.Outputs[tx.Inputs[0].PrevOut.N].ScriptPubKey;
                        scriptFilled = true;
                        break;
                    }
                }
            }
            if (!scriptFilled && coinView != null)
            {
                FetchCoinsResponse fetchCoinResult = coinView.FetchCoinsAsync(new uint256[] { tx.Inputs[0].PrevOut.Hash }).Result;
                script = fetchCoinResult.UnspentOutputs.FirstOrDefault().Outputs[0].ScriptPubKey;
                scriptFilled = true;
            }

            if (new PayToPubkeyTemplate().CheckScriptPubKey(script))
            {
                return new uint160(script.GetDestinationPublicKeys().FirstOrDefault().Hash.ToBytes(), false);
            }

            if (new PayToPubkeyHashTemplate().CheckScriptPubKey(script))
            {
                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }
    }
}
