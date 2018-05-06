using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.FederatedPeg.Features.SidechainRuntime.Models
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
    public class WithdrawFundsFromSidechainRequest : BuildTransactionRequest
    {
        [Required(ErrorMessage = "A sidechain name is required.")]
        public string SidechainName { get; set; }

        [Required(ErrorMessage = "A destination address on the sidechain is required.")]
        public string MainchainDestinationAddress { get; set; }
    }
}
