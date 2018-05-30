using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Controllers.Models
{
    public class ValidateAddressRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        public string address { get; set; }
    }
}
