using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class PaymentCollection : ICollection<PaymentDetails>
    {
        private bool isChange;
        private List<PaymentDetails> payments;
        private SpendingDetails spendingDetails;
        private TransactionData transactionData => this.spendingDetails?.TransactionData;
        private HdAccount account => this.transactionData?.TransactionCollection?.Address?.AddressCollection?.Account;
        private Wallet wallet => this.account?.AccountRoot?.Wallet;
        private IWalletRepository repository => this.wallet?.WalletRepository;

        public int Count => this.GetPayments().Count();
        public bool IsReadOnly => true;

        private IEnumerable<PaymentDetails> GetPayments()
        {
            // TODO: if (this.payments == null)
            {
                if (this.repository != null)
                    this.payments = this.repository.GetPaymentDetails(this.wallet.Name, this.transactionData, this.isChange).ToList();
                else
                    this.payments = this.payments ?? new List<PaymentDetails>();
            }

            return this.payments;
        }

        public PaymentCollection(SpendingDetails spendingDetails, ICollection<PaymentDetails> payments, bool isChange)
        {
            this.isChange = isChange;
            this.spendingDetails = spendingDetails;
            this.payments = payments?.ToList() ?? new List<PaymentDetails>();
        }

        public void Add(PaymentDetails payment)
        {
            if (this.repository == null)
                this.payments.Add(payment);
            else
                throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(PaymentDetails payment)
        {
            return this.payments.Contains(payment);
        }

        public void CopyTo(PaymentDetails[] arr, int index)
        {
            foreach (PaymentDetails payment in this.GetPayments())
                arr[index++] = payment;
        }

        public bool Remove(PaymentDetails paymentDetails)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<PaymentDetails> GetEnumerator()
        {
            return this.GetPayments().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetPayments().GetEnumerator();
        }
    }

    public class SpendingDetails
    {
        public TransactionData TransactionData { get; set; }

        public SpendingDetails()
        {
            this.Payments = new PaymentCollection(this, new List<PaymentDetails>(), false);
            this.Change = new PaymentCollection(this, new List<PaymentDetails>(), true);
        }

        [JsonConstructor]
        public SpendingDetails(ICollection<PaymentDetails> payments, ICollection<PaymentDetails> change)
        {
            this.Payments = new PaymentCollection(this, payments, false);
            this.Change = new PaymentCollection(this, change, true);
        }

        /// <summary>
        /// The id of the transaction in which the output referenced in this transaction is spent.
        /// </summary>
        [JsonProperty(PropertyName = "transactionId", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionId { get; set; }

        /// <summary>
        /// A list of payments made out in this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "payments", NullValueHandling = NullValueHandling.Ignore)]
        public ICollection<PaymentDetails> Payments { get; set; }

        [JsonProperty(PropertyName = "change", NullValueHandling = NullValueHandling.Ignore)]
        public ICollection<PaymentDetails> Change { get; set; }

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight { get; set; }

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
        public uint256 BlockHash { get; set; }

        /// <summary>
        /// The index of this transaction in the block in which it is contained.
        /// </summary>
        [JsonProperty(PropertyName = "blockIndex", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockIndex { get; set; }

        /// <summary>
        /// A value indicating whether this is a coin stake transaction or not.
        /// </summary>
        [JsonProperty(PropertyName = "isCoinStake", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsCoinStake { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// Hexadecimal representation of this spending transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex { get; set; }

        /// <summary>
        /// Determines whether this transaction being spent is confirmed.
        /// </summary>
        public bool IsSpentConfirmed()
        {
            return this.BlockHeight != null;
        }
    }
}