using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public class GetBlockHashRequestModel
    {
        public int height { get; set; }
    }
}
