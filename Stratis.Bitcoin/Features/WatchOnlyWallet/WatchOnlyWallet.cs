using System;
using System.Collections.Concurrent;
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
            this.WatchedAddresses = new ConcurrentDictionary<string, WatchedAddress>();
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
        [JsonConverter(typeof(WatchedAddressesConcurrentDictionaryConverter))]
        public ConcurrentDictionary<string, WatchedAddress> WatchedAddresses { get; set; }
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
            this.Transactions = new ConcurrentDictionary<string, TransactionData>();
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
        /// This is a convenience property whose intrisic value is equal to <see cref="WatchedAddress.Script"/>.
        /// </remarks>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// The list of transactions affecting the <see cref="Script"/> being watched.
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        [JsonConverter(typeof(TransactionDataConcurrentDictionaryConverter))]
        public ConcurrentDictionary<string, TransactionData> Transactions { get; set; }

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

    #region Json Converters

    /// <summary>
    /// Converter used to convert a <see cref="ConcurrentDictionary{TKey,TValue}"/> (where TKey is <see cref="string"/> and TValue is <see cref="WatchedAddress"/>) to and from a collection of its values.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class WatchedAddressesConcurrentDictionaryConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            // Check this is a ConcurrentDictionary with the right argument types.
            return objectType == typeof(ConcurrentDictionary<string, WatchedAddress>);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            IEnumerable<WatchedAddress> watchedAddesses = serializer.Deserialize<IEnumerable<WatchedAddress>>(reader);

            ConcurrentDictionary<string, WatchedAddress> watchedAddressesDictionary = new ConcurrentDictionary<string, WatchedAddress>();
            foreach (var watchedAddress in watchedAddesses)
            {
                watchedAddressesDictionary.TryAdd(watchedAddress.Script.ToString(), watchedAddress);
            }

            return watchedAddressesDictionary;
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            ConcurrentDictionary<string, WatchedAddress> watchedAddressesDictionary = (ConcurrentDictionary<string, WatchedAddress>)value;
            serializer.Serialize(writer, watchedAddressesDictionary.Values);
        }
    }

    /// <summary>
    /// Converter used to convert a <see cref="ConcurrentDictionary{TKey,TValue}"/> (where TKey is <see cref="string"/> and TValue is <see cref="TransactionData"/>) to and from a collection of its values.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class TransactionDataConcurrentDictionaryConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            // Check this is a ConcurrentDictionary with the right argument types.
            return objectType == typeof(ConcurrentDictionary<string, TransactionData>);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            IEnumerable<TransactionData> transactions = serializer.Deserialize<IEnumerable<TransactionData>>(reader);

            ConcurrentDictionary<string, TransactionData> transactionsDictionary = new ConcurrentDictionary<string, TransactionData>();
            foreach (var transaction in transactions)
            {
                transactionsDictionary.TryAdd(transaction.Transaction.GetHash().ToString(), transaction);
            }

            return transactionsDictionary;
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            ConcurrentDictionary<string, TransactionData> transactionsDictionary = (ConcurrentDictionary<string, TransactionData>)value;
            serializer.Serialize(writer, transactionsDictionary.Values);
        }
    }

    #endregion    
}