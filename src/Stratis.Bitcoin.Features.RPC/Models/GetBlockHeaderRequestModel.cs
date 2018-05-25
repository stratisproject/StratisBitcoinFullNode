using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public class GetBlockHeaderRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        public string hash { get; set; }

        public bool isJsonFormat { get; set; }
    }
}
