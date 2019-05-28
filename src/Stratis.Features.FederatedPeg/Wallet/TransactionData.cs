using System;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;
using TracerAttributes;

namespace Stratis.Features.FederatedPeg.Wallet
{
    public interface ITransactionDataObserver
    {
        void BeforeSpendingDetailsChanged(TransactionData transactionData);
        void AfterSpendingDetailsChanged(TransactionData transactionData);
    }

    public interface ITransactionDataObservable
    {
        void Subscribe(ITransactionDataObserver observer);
    }

    /// <summary>
    /// An object containing transaction data.
    /// </summary>
    public class TransactionData : ITransactionDataObservable
    {
        ITransactionDataObserver parent;

        [JsonProperty(PropertyName = "id")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Id { get; set; }

        [JsonProperty(PropertyName = "amount")]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }

        /// <summary>
        /// The index of this scriptPubKey in the transaction it is contained.
        /// </summary>
        /// <remarks>
        /// This is effectively the index of the output, the position of the output in the parent transaction.
        /// </remarks>
        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public int Index { get; set; }

        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight { get; set; }

        [JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// Hexadecimal representation of this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex { get; set; }

        public OutPoint Key => new OutPoint(this.Id, this.Index);

        private SpendingDetails spendingDetails;

        [JsonProperty(PropertyName = "spendingDetails", NullValueHandling = NullValueHandling.Ignore)]
        public SpendingDetails SpendingDetails
        {
            get
            {
                return this.spendingDetails;
            }

            set
            {
                this.parent?.BeforeSpendingDetailsChanged(this);
                this.spendingDetails = value;
                this.parent?.AfterSpendingDetailsChanged(this);
            }
        }

        public bool IsConfirmed()
        {
            return this.BlockHeight != null;
        }

        [NoTrace]
        public Transaction GetFullTransaction(Network network)
        {
            return network.CreateTransaction(this.Hex);
        }

        public bool IsSpendable()
        {
            // TODO: Coinbase maturity check?
            return this.SpendingDetails == null;
        }

        public Money SpendableAmount(bool confirmedOnly)
        {
            // This method only returns a UTXO that has no spending output.
            // If a spending output exists (even if its not confirmed) this will return as zero balance.
            if (!this.IsSpendable()) return Money.Zero;

            if (confirmedOnly && !this.IsConfirmed())
            {
                return Money.Zero;
            }

            return this.Amount;
        }

        public void Subscribe(ITransactionDataObserver observer)
        {
            this.parent = observer;
        }
    }
}