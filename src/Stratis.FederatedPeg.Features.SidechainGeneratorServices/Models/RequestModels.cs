using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Stratis.FederatedPeg.Features.SidechainGeneratorServices.Models
{
    public class RequestModel
    {
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    /// <summary>
    /// Properties required to create the Redeem script and address.
    /// </summary>
    public class OutputScriptPubKeyAndAddressRequest : RequestModel
    {
        [Required(ErrorMessage = "The multi-sig N value is required.")]
        public int MultiSigN { get; set; }

        [Required(ErrorMessage = "The multi-sig N value is required.")]
        public int MultiSigM { get; set; }

        [Required(ErrorMessage = "The path to the local folder where the federation keys are stored is required.")]
        public string FederationFolder { get; set; }
    }

    /// <summary>
    /// Properties required to mine the premine.
    /// </summary>
    public class MinePremineRequest : RequestModel
    {
        [Required(ErrorMessage = "The address is required.")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Number of blocks is required.")]
        public ulong NumberOfBlocks { get; set; }
    }
}
