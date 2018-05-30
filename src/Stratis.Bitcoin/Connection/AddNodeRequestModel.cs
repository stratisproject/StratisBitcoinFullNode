using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Connection
{
    public class AddNodeRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Endpoint { get; set; }
        [Required(AllowEmptyStrings = false)]
        public string Command { get; set; }
    }
}
