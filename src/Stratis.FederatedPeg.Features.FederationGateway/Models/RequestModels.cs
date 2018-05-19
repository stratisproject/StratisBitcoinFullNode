using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    public class JsonContent : StringContent
    {
        public JsonContent(object obj) :
            base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json")
        {

        }
    }

    public class RequestModel
    {
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    /// <summary>
    /// Used to create a session that builds a multi-sig transaction by requesting
    /// signing from other Federation nodes and then broadcasts the transaction.
    /// </summary>
    public class CreatePartialTransactionSessionRequest : RequestModel
    {
        /// <summary>
        /// An Id used to identify the session. (Since sessions are currently 'per transaction' the hash of the
        /// originating deposit or withdrawal transaction is used as the sessionId).
        /// </summary>
        [Required(ErrorMessage = "SessionId must be specified.")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 SessionId { get; set; }

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
}
