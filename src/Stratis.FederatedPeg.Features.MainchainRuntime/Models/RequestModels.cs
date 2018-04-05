using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;

//This is experimental while we are waiting for a generic OP_RETURN function in the full node wallet.

namespace Stratis.FederatedPeg.Features.MainchainRuntime.Models
{
    public class RequestModel
    {
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    /// <summary>
    /// Used to initialize a sidechain
    /// </summary>
    public class SendFundsToSidechainRequest : BuildTransactionRequest
    {
        [Required(ErrorMessage = "A sidechain name is required.")]
        public string SidechainName { get; set; }

        [Required(ErrorMessage = "A destination address on the sidechain is required.")]
        public string SidechainDestinationAddress { get; set; }
    }
}
