using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Stratis.FederatedPeg.Features.MainchainGeneratorServices.Models
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
    /// Properties used to initialize a sidechain.
    /// </summary>
    public class InitSidechainRequest : RequestModel
    {
        [Required(ErrorMessage = "A sidechain name is required.")]
        public string SidechainName { get; set; }

        [Required(ErrorMessage = "The ApiPort the sidechain uses is required.")]
        public int ApiPortForSidechain  { get; set; }

        [Required(ErrorMessage = "The multi-sig M value is required.")]
        public int MultiSigM { get; set; }

        [Required(ErrorMessage = "The multi-sig N value is required.")]
        public int MultiSigN { get; set; }

        [Required(ErrorMessage = "The path to the local folder where the federation keys are stored is required.")]
        public string FolderFedMemberKeys { get; set; }
    }


    //ToDo: Consider moving these common request objects to Stratis.FederatedPeg.
    /// <summary>
    /// Properties required to create the Redeem script and address.
    /// </summary>
    public class OutputScriptPubKeyAndAddressRequest : RequestModel
    {
        [Required(ErrorMessage = "The multi-sig M value is required.")]
        public int MultiSigM { get; set; }

        [Required(ErrorMessage = "The multi-sig N value is required.")]
        public int MultiSigN { get; set; }

        [Required(ErrorMessage = "The path to the local folder where the federation keys are stored is required.")]
        public string FederationFolder { get; set; }
    }
}
