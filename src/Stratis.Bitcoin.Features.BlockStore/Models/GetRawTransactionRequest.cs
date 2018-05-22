using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public class GetRawTransactionRequest : RequestBase
    {
        [Required(AllowEmptyStrings = false)]
        public string txid { get; set; }

        public int verbose { get; set; }
    }
}