using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Controllers.Models
{
    public class GetBlockHeaderRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        public string hash { get; set; }

        public bool isJsonFormat { get; set; }
    }
}
