using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public class AddNodeRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Endpoint { get; set; }
        [Required(AllowEmptyStrings = false)]
        public string command { get; set; }
    }
}
