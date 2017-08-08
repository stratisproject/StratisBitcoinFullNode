using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.JsonConverters;
using Script = NBitcoin.Script;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet
{
    /// <summary>
    /// Represents a watch-only wallet. 
    /// </summary>
    public class WatchOnlyWallet
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WatchOnlyWallet"/> class.
        /// </summary>
        public WatchOnlyWallet()
        {
            this.WatchedAddresses = new List<WatchedAddress>();
        }

        /// <summary>
        /// The network this wallet is for.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        public Network Network { get; set; }

        /// <summary>
        /// The type of coin, Bitcoin or Stratis.
        /// </summary>
        [JsonProperty(PropertyName = "coinType")]
        public CoinType CoinType { get; set; }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// The list of addresses being watched.
        /// </summary>
        [JsonProperty(PropertyName = "watchedAddresses")]
        public ICollection<WatchedAddress> WatchedAddresses { get; set; }
    }

    /// <summary>
    /// An object contaning a <see cref="Script"/> being watched along with any transactions affecting it.
    /// </summary>
    public class WatchedAddress
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WatchedAddress"/> class.
        /// </summary>
        public WatchedAddress()
        {
            this.Transactions = new List<TransactionData>();
        }

        /// <summary>
        /// A <see cref="Script"/> being watched for transactions affecting it.
        /// </summary>
        [JsonProperty(PropertyName = "script")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script Script { get; set; }

        /// <summary>
        /// A base58 address being watched for transactions affecting it.
        /// </summary>
        /// <remarks>
        /// This is a convenience property whose intrisic value is equal to <see cref="Script"/>.
        /// </remarks>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// The list of transactions affecting the <see cref="Script"/> being watched.
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public ICollection<TransactionData> Transactions { get; set; }

    }

    /// <summary>
    /// An object containing the details of a transaction affecting a <see cref="Script"/> being watched.
    /// </summary>
    public class TransactionData
    {
        /// <summary>
        /// Hexadecimal representation of a transaction affecting a script being watched.
        /// </summary>
        [JsonProperty(PropertyName = "hex")]
        public string Hex { get; set; }

        /// <summary>
        /// A transaction affecting a script being watched.
        /// </summary>
        [JsonIgnore]
        public Transaction Transaction => Transaction.Parse(this.Hex);

        /// <summary>
        /// The hash of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }
    }
}