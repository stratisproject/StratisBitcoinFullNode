using System;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;
using TracerAttributes;

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// Defines the interface to be supported by observers of <see cref="TransactionData"/>.
    /// </summary>
    public interface ITransactionDataObserver : ILockProtected
    {
        void BeforeSpendingDetailsChanged(TransactionData transactionData);
        void AfterSpendingDetailsChanged(TransactionData transactionData);
        void BeforeBlockHeightChanged(TransactionData transactionData);
        void AfterBlockHeightChanged(TransactionData transactionData);
    }

    /// <summary>
    /// Defines the interface to be supported by the <see cref="TransactionData"/> observable.
    /// </summary>
    public interface ITransactionDataObservable
    {
        /// <summary>
        /// Used by the parent/container object to subscribe to changes in the <see cref="TransactionData"/> child object.
        /// </summary>
        /// <param name="observer">The parent that wishes to observe the child.</param>
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

        private int? blockHeight;

        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight
        {
            get
            {
                return this.blockHeight;
            }

            set
            {
                if (this.parent != null)
                {
                    this.parent.Synchronous(() =>
                    {
                        this.parent.BeforeBlockHeightChanged(this);
                        this.blockHeight = value;
                        this.parent.AfterBlockHeightChanged(this);
                    });
                }
                else
                {
                    this.blockHeight = value;
                }
            }
        }

        [JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        [JsonIgnore]
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
                if (this.parent != null)
                {
                    this.parent.Synchronous(() =>
                    {
                        this.parent.BeforeSpendingDetailsChanged(this);
                        this.spendingDetails = value;
                        this.parent.AfterSpendingDetailsChanged(this);
                    });
                }
                else
                {
                    this.spendingDetails = value;
                }
            }
        }

        [NoTrace]
        public bool IsConfirmed()
        {
            return this.BlockHeight != null;
        }

        [NoTrace]
        public bool IsSpendable()
        {
            // TODO: Coinbase maturity check?
            return this.SpendingDetails == null;
        }

        [NoTrace]
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
