using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>
    /// A model class to be returned when the user sends a transaction successfully.
    /// </summary>
    public class WalletSendTransactionModel
    {
        /// <summary>
        /// The transaction id.
        /// </summary>
        [JsonProperty(PropertyName = "transactionId")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionId { get; set; }

        /// <summary>
        /// The list of outputs in this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "outputs")]
        public ICollection<TransactionOutputModel> Outputs { get; set; }
    }

    /// <summary>
    /// A simple transaction output.
    /// </summary>
    public class TransactionOutputModel
    {
        /// <summary>
        /// The output address in Base58.
        /// </summary>
        [JsonProperty(PropertyName = "address", NullValueHandling = NullValueHandling.Ignore)]
        public string Address { get; set; }

        /// <summary>
        /// The amount associated with the output.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        public Money Amount { get; set; }

        /// <summary>
        /// The data encoded in the OP_RETURN script
        /// </summary>
        [JsonProperty(PropertyName = "opReturnData", NullValueHandling = NullValueHandling.Ignore)]
        public string OpReturnData { get; set; }
    }
}
