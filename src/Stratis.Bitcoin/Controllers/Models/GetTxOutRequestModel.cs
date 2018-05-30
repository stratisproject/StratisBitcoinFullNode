using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Controllers.Models
{
    public class GetTxOutRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        public string txid { get; set; }
        public string vout { get; set; } = "0";
        public bool includeMemPool { get; set; } = true;
    }
}
