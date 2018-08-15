using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core.Util
{
    /// <summary>
    /// To run smart contracts we need to have a 'From' address. This isn't natively included in a bitcoin transaction
    /// so to get it we check the script of the PrevOut to get an address we can use as 'From'
    /// </summary>
    public static class GetSenderUtil
    {
        /// <summary>
        /// Get the 'sender' of a transaction.
        /// </summary>
        /// <param name="tx"></param>
        /// <param name="coinView"></param>
        /// <param name="blockTxs"></param>
        /// <returns></returns>
        public static GetSenderResult GetSender(Transaction tx, ICoinView coinView, IList<Transaction> blockTxs)
        {
            OutPoint prevOut = tx.Inputs[0].PrevOut;

            // Check the txes in this block first
            if (blockTxs != null && blockTxs.Count > 0)
            {
                foreach (Transaction btx in blockTxs)
                {
                    if (btx.GetHash() == prevOut.Hash)
                    {
                        Script script = btx.Outputs[prevOut.N].ScriptPubKey;
                        return GetAddressFromScript(script);
                    }
                }
            }

            // Check the utxoset for the p2pk of the unspent output for this transaction
            if (coinView != null)
            {
                FetchCoinsResponse fetchCoinResult = coinView.FetchCoinsAsync(new uint256[] { prevOut.Hash }).Result;
                UnspentOutputs unspentOutputs = fetchCoinResult.UnspentOutputs.FirstOrDefault();

                if (unspentOutputs == null)
                {
                    throw new Exception("Unspent outputs to smart contract transaction are not present in coinview");
                }

                Script script = unspentOutputs.Outputs[prevOut.N].ScriptPubKey;

                return GetAddressFromScript(script);
            }

            return GetSenderResult.CreateFailure("Unable to get the sender of the transaction");
        }

        /// <summary>
        /// Get the address from a P2PK or a P2PKH
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public static GetSenderResult GetAddressFromScript(Script script)
        {
            PubKey payToPubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(script);

            if (payToPubKey != null)
            {
                var address = new uint160(payToPubKey.Hash.ToBytes());
                return GetSenderResult.CreateSuccess(address);
            }

            if (PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(script))
            {
                var address = new uint160(PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(script).ToBytes());
                return GetSenderResult.CreateSuccess(address);
            }
            return GetSenderResult.CreateFailure("Addresses can only be retrieved from Pay to Pub Key or Pay to Pub Key Hash");
        }

        public class GetSenderResult
        {
            private GetSenderResult(uint160 address)
            {
                this.Success = true;
                this.Sender = address;
            }

            private GetSenderResult(string error)
            {
                this.Success = false;
                this.Error = error;
            }

            public bool Success { get; }

            public uint160 Sender { get; }

            public string Error { get; }

            public static GetSenderResult CreateSuccess(uint160 address)
            {
                return new GetSenderResult(address);
            }

            public static GetSenderResult CreateFailure(string error)
            {
                return new GetSenderResult(error);
            }
        }
    }
}