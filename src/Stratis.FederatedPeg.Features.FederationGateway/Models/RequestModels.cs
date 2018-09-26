using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// Helper class to interpret a string as json.
    /// </summary>
    public class JsonContent : StringContent
    {
        public JsonContent(object obj) :
            base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json")
        {

        }
    }

    /// <summary>
    /// Used to create a session that builds a multi-sig transaction by requesting
    /// signing from other Federation nodes and then broadcasts the transaction.
    /// </summary>
    public class CreateCounterChainSessionRequest : RequestModel
    {
        public List<CounterChainTransactionInfoRequest> CounterChainTransactionInfos { get; set; }

        /// <summary>
        /// Number of the block at which the countersession was initiated
        /// </summary>
        [Required(ErrorMessage = "BlockHeight needs to be specified.")]
        [Range(0, int.MaxValue, ErrorMessage = "Invalid BlockHeight")]
        public int BlockHeight { get; set; }
    }

    public class CounterChainTransactionInfoRequest : RequestModel
    {
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionHash { get; set; }

        /// <summary>
        /// The amount of the transaction.
        /// </summary>
        [Required(ErrorMessage = "An amount required.")]
        public string Amount { get; set; }

        /// <summary>
        /// The final destination address of the user to receive the funds. For a deposit this is a user address on the sidechain,
        /// for a withdrawal it is an address on the mainchain.
        /// </summary>
        [Required(ErrorMessage = "Destination Address required.")]
        public string DestinationAddress { get; set; }

    }

    public class ImportMemberKeyRequest : RequestModel
    {
        [Required(ErrorMessage = "A mnemonic is required.")]
        public string Mnemonic { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }
    }

    /// <summary>
    /// Model for the "enablefederation" request.
    /// </summary>
    public class EnableFederationRequest : RequestModel
    {
        /// <summary>
        /// The federation wallet password.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }
    }
}
