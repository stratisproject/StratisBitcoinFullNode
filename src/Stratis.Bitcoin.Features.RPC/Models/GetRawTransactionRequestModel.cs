using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public class GetRawTransactionRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        public string txid { get; set; }
        public bool verbose { get; set; } = false;
    }
}
