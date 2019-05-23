using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Features.FederatedPeg.Models
{
    /// <summary>
    /// Model for the "enablefederation" request.
    /// </summary>
    public class EnableFederationRequest : RequestModel
    {
        public string Mnemonic { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        public string Passphrase { get; set; }

        public int? TimeoutSeconds { get; set; }
    }

    /// <summary>
    /// Model object to use as input to the Api request for removing transactions from a wallet.
    /// </summary>
    /// <seealso cref="RequestModel" />
    public class RemoveFederationTransactionsModel : RequestModel
    {
        [Required(ErrorMessage = "The reSync flag is required.")]
        [JsonProperty(PropertyName = "reSync")]
        public bool ReSync { get; set; }
    }
}
