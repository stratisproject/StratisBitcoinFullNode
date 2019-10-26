using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.SQLiteWalletRepository.External
{
    public interface ITransactionsToLists
    {
        bool ProcessTransactions(IEnumerable<Transaction> transactions, HashHeightPair block, uint256 fixedTxId = null);
    }

    public abstract class TransactionsToListsBase : ITransactionsToLists
    {
        private readonly Network network;
        private readonly IScriptAddressReader scriptAddressReader;
        protected readonly IWalletTransactionLookup transactionsOfInterest;
        protected readonly IWalletAddressLookup addressesOfInterest;

        public abstract ITopUpTracker GetTopUpTracker(AddressIdentifier address);
        public abstract void RecordSpend(HashHeightPair block, TxIn txIn, string pubKeyScript, bool isCoinBase, long spendTime, Money totalOut, uint256 spendTxId, int spendIndex);
        public abstract void RecordReceipt(HashHeightPair block, Script pubKeyScript, TxOut txOut, bool isCoinBase, long creationTime, uint256 outputTxId, int outputIndex, bool isChange);

        public TransactionsToListsBase(Network network, IScriptAddressReader scriptAddressReader, IWalletTransactionLookup transactionsOfInterest, IWalletAddressLookup addressesOfInterest)
        {
            this.network = network;
            this.scriptAddressReader = scriptAddressReader;
            this.transactionsOfInterest = transactionsOfInterest;
            this.addressesOfInterest = addressesOfInterest;
        }

        protected IEnumerable<Script> GetDestinations(Script redeemScript)
        {
            ScriptTemplate scriptTemplate = this.network.StandardScriptsRegistry.GetTemplateFromScriptPubKey(redeemScript);

            if (scriptTemplate != null)
            {
                // We need scripts suitable for matching to HDAddress.ScriptPubKey.
                switch (scriptTemplate.Type)
                {
                    case TxOutType.TX_PUBKEYHASH:
                        yield return redeemScript;
                        break;
                    case TxOutType.TX_PUBKEY:
                        yield return PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(redeemScript).Hash.ScriptPubKey;
                        break;
                    case TxOutType.TX_SCRIPTHASH:
                        yield return PayToScriptHashTemplate.Instance.ExtractScriptPubKeyParameters(redeemScript).ScriptPubKey;
                        break;
                    default:
                        if (this.scriptAddressReader is ScriptDestinationReader scriptDestinationReader)
                        {
                            foreach (TxDestination destination in scriptDestinationReader.GetDestinationFromScriptPubKey(this.network, redeemScript))
                            {
                                yield return destination.ScriptPubKey;
                            }
                        }
                        else
                        {
                            string address = this.scriptAddressReader.GetAddressFromScriptPubKey(this.network, redeemScript);
                            TxDestination destination = ScriptDestinationReader.GetDestinationForAddress(address, this.network);
                            if (destination != null)
                                yield return destination.ScriptPubKey;
                        }

                        break;
                }
            }
        }

        public bool ProcessTransactions(IEnumerable<Transaction> transactions, HashHeightPair block, uint256 fixedTxId = null)
        {
            bool additions = false;

            // Convert relevant information in the block to information that can be joined to the wallet tables.
            IWalletTransactionLookup transactionsOfInterest = this.transactionsOfInterest;
            IWalletAddressLookup addressesOfInterest = this.addressesOfInterest;

            // Used for tracking address top-up requirements.
            var trackers = new Dictionary<TopUpTracker, TopUpTracker>();

            foreach (Transaction tx in transactions)
            {
                // Build temp.PrevOuts
                uint256 txId = fixedTxId ?? tx.GetHash();
                bool addSpendTx = false;

                for (int i = 0; i < tx.Inputs.Count; i++)
                {
                    TxIn txIn = tx.Inputs[i];

                    if (transactionsOfInterest.Contains(txIn.PrevOut, out HashSet<AddressIdentifier> addresses))
                    {
                        // Record our outputs that are being spent.
                        foreach (AddressIdentifier address in addresses)
                            RecordSpend(block, txIn, address.ScriptPubKey, tx.IsCoinBase | tx.IsCoinStake, tx.Time, tx.TotalOut, txId, i);

                        additions = true;
                        addSpendTx = true;
                    }
                }

                // Build temp.Outputs.
                for (int i = 0; i < tx.Outputs.Count; i++)
                {
                    TxOut txOut = tx.Outputs[i];

                    if (txOut.IsEmpty)
                        continue;

                    if (txOut.ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_RETURN)
                        continue;

                    foreach (Script pubKeyScript in this.GetDestinations(txOut.ScriptPubKey))
                    {
                        bool containsAddress = addressesOfInterest.Contains(pubKeyScript, out AddressIdentifier address);

                        // Paying to one of our addresses?
                        if (addSpendTx || containsAddress)
                        {
                            // Check if top-up is required.
                            if (containsAddress && address != null)
                            {
                                // Get the top-up tracker that applies to this account and address type.
                                ITopUpTracker tracker = this.GetTopUpTracker(address);
                                if (!tracker.IsWatchOnlyAccount)
                                {
                                    // If an address inside the address buffer is being used then top-up the buffer.
                                    while (address.AddressIndex >= tracker.NextAddressIndex)
                                    {
                                        AddressIdentifier newAddress = tracker.CreateAddress();

                                        // Add the new address to our addresses of interest.
                                        addressesOfInterest.AddTentative(Script.FromHex(newAddress.ScriptPubKey),
                                            new AddressIdentifier()
                                            {
                                                WalletId = newAddress.WalletId,
                                                AccountIndex = newAddress.AccountIndex,
                                                AddressType = newAddress.AddressType,
                                                AddressIndex = newAddress.AddressIndex
                                            });
                                    }
                                }
                            }

                            // Record outputs received by our wallets.
                            this.RecordReceipt(block, pubKeyScript, txOut, tx.IsCoinBase | tx.IsCoinStake, tx.Time, txId, i, containsAddress && address.AddressType == 1);

                            additions = true;

                            if (containsAddress)
                                transactionsOfInterest.AddTentative(new OutPoint(txId, i), address);
                        }
                    }
                }
            }

            return additions;
        }
    }
}
