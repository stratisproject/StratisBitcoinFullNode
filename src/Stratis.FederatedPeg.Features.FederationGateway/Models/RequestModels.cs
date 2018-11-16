using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;

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

    /// <summary>
    /// Block tip Hash and Height request.
    /// </summary>
    public class BlockTipModelRequest : RequestModel
    {
        [Required(ErrorMessage = "Block Hash is required")]
        public string Hash { get; set; }

        [Required(ErrorMessage = "Block Height is required")]
        public int Height { get; set; }
    }
}
