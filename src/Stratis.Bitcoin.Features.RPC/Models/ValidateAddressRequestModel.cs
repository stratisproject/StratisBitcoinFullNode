using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public class ValidateAddressRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        public string address { get; set; }
    }
}
