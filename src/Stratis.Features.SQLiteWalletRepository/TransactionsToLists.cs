using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository.External;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    public class TransactionsToLists : TransactionsToListsBase
    {
        private readonly DBConnection conn;
        private readonly ProcessBlocksInfo processBlocksInfo;

        internal TransactionsToLists(Network network, IScriptAddressReader scriptAddressReader, ProcessBlocksInfo processBlocksInfo)
            : base(network, scriptAddressReader, processBlocksInfo.TransactionsOfInterest, processBlocksInfo.AddressesOfInterest)
        {
            this.conn = processBlocksInfo.Conn;
            this.processBlocksInfo = processBlocksInfo;
        }

        public override void RecordSpend(HashHeightPair block, TxIn txIn, string pubKeyScript, bool isCoinBase, long spendTime, Money totalOut, uint256 spendTxId, int spendIndex)
        {
            var prevOut = new TempPrevOut()
            {
                OutputTxId = txIn.PrevOut.Hash.ToString(),
                OutputIndex = (int)txIn.PrevOut.N,
                ScriptPubKey = pubKeyScript,
                SpendBlockHeight = block?.Height,
                SpendBlockHash = block?.Hash?.ToString(),
                SpendTxIsCoinBase = isCoinBase ? 1 : 0,
                SpendTxTime = spendTime,
                SpendTxId = spendTxId.ToString(),
                SpendIndex = spendIndex,
                SpendTxTotalOut = totalOut.Satoshi
            });

            this.conn.Repository.logger.LogTrace("Recording spend: {0}", prevOut.ToString());
            this.processBlocksInfo.PrevOuts.Add(prevOut);
        }

        public override void RecordReceipt(HashHeightPair block, Script pubKeyScript, TxOut txOut, bool isCoinBase, long creationTime, uint256 outputTxId, int outputIndex, bool isChange)
        {
            // Record outputs received by our wallets.
            var output = new TempOutput()
            {
                // For matching HDAddress.ScriptPubKey.
                ScriptPubKey = pubKeyScript?.ToHex(),

                // The ScriptPubKey from the txOut.
                RedeemScript = txOut.ScriptPubKey.ToHex(),

                Address = pubKeyScript?.GetDestinationAddress(this.conn.Repository.Network).ToString() ?? "",
                OutputBlockHeight = block?.Height,
                OutputBlockHash = block?.Hash.ToString(),
                OutputTxIsCoinBase = isCoinBase ? 1 : 0,
                OutputTxTime = creationTime,
                OutputTxId = outputTxId.ToString(),
                OutputIndex = outputIndex,
                Value = txOut.Value.Satoshi,
                IsChange = isChange ? 1 : 0
            };

            this.conn.Repository.logger.LogTrace("Recording receipt: {0}", output.ToString());
            this.processBlocksInfo.Outputs.Add(output);
        }

        public override ITopUpTracker GetTopUpTracker(AddressIdentifier address)
        {
            var key = new TopUpTracker(this.processBlocksInfo, address.WalletId, (int)address.AccountIndex, (int)address.AddressType);
            if (!this.processBlocksInfo.Trackers.TryGetValue(key, out TopUpTracker tracker))
            {
                tracker = key;
                tracker.ReadAccount();
                this.processBlocksInfo.Trackers.Add(tracker, tracker);
            }

            return tracker;
        }
    }
}
