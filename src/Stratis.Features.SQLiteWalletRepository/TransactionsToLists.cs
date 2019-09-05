using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Features.SQLiteWalletRepository.External;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    public class TransactionsToLists : TransactionsToListsBase
    {
        private readonly DBConnection conn;
        private readonly ProcessBlocksInfo processBlocksInfo;
        private readonly Dictionary<TopUpTracker, TopUpTracker> trackers;

        internal TransactionsToLists(Network network, IScriptAddressReader scriptAddressReader, ProcessBlocksInfo processBlocksInfo)
            : base(network, scriptAddressReader, processBlocksInfo.TransactionsOfInterest, processBlocksInfo.AddressesOfInterest)
        {
            this.conn = processBlocksInfo.Conn;
            this.processBlocksInfo = processBlocksInfo;
            this.trackers = new Dictionary<TopUpTracker, TopUpTracker>();
        }

        public override void RecordSpend(ChainedHeader header, TxIn txIn, AddressIdentifier address, Transaction spendTx, uint256 spendTxId, int spendIndex)
        {
            this.processBlocksInfo.PrevOuts.Add(new TempPrevOut()
            {
                OutputTxId = txIn.PrevOut.Hash.ToString(),
                OutputIndex = (int)txIn.PrevOut.N,
                ScriptPubKey = address.ScriptPubKey,
                SpendBlockHeight = header?.Height ?? 0,
                SpendBlockHash = header?.HashBlock.ToString(),
                SpendTxIsCoinBase = (spendTx.IsCoinBase || spendTx.IsCoinStake) ? 1 : 0,
                SpendTxTime = (int)spendTx.Time,
                SpendTxId = spendTxId.ToString(),
                SpendIndex = spendIndex,
                SpendTxTotalOut = spendTx.TotalOut.ToDecimal(MoneyUnit.BTC)
            });
        }

        public override void RecordReceipt(ChainedHeader header, Script pubKeyScript, TxOut txOut, Transaction outputTx, uint256 outputTxId, int outputIndex)
        {
            // Record outputs received by our wallets.
            this.processBlocksInfo.Outputs.Add(new TempOutput()
            {
                // For matching HDAddress.ScriptPubKey.
                ScriptPubKey = pubKeyScript.ToHex(),

                // The ScriptPubKey from the txOut.
                RedeemScript = txOut.ScriptPubKey.ToHex(),

                OutputBlockHeight = header?.Height ?? 0,
                OutputBlockHash = header?.HashBlock.ToString(),
                OutputTxIsCoinBase = (outputTx.IsCoinBase || outputTx.IsCoinStake) ? 1 : 0,
                OutputTxTime = (int)outputTx.Time,
                OutputTxId = outputTxId.ToString(),
                OutputIndex = outputIndex,
                Value = txOut.Value.ToDecimal(MoneyUnit.BTC)
            });
        }

        public override ITopUpTracker GetTopUpTracker(AddressIdentifier address)
        {
            var key = new TopUpTracker(address.WalletId, address.AccountIndex, address.AddressType);
            if (!this.trackers.TryGetValue(key, out TopUpTracker tracker))
            {
                tracker = key;
                tracker.ReadAccount(this.conn);
                this.trackers.Add(tracker, tracker);
            }

            return tracker;
        }

        public override AddressIdentifier CreateAddress(ITopUpTracker tracker)
        {
            HDAddress newAddress = this.conn.CreateAddress(((TopUpTracker)tracker).Account, tracker.AddressType, tracker.AddressCount);

            if (!this.conn.IsInTransaction)
            {
                // We've postponed creating a transaction since we weren't sure we will need it.
                // Create it now.
                this.conn.BeginTransaction();
                this.processBlocksInfo.MustCommit = true;
            }

            // Insert the new address into the database.
            this.conn.Insert(newAddress);

            // Update the information in the tracker.
            ((TopUpTracker)tracker).NextAddressIndex++;
            ((TopUpTracker)tracker).AddressCount++;

            return new AddressIdentifier()
            {
                WalletId = newAddress.WalletId,
                AccountIndex = newAddress.AccountIndex,
                AddressType = newAddress.AddressType,
                AddressIndex = newAddress.AddressIndex,
                ScriptPubKey = newAddress.ScriptPubKey
            };
        }
    }
}
