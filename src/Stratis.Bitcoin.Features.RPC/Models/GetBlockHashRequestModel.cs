using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public class GetBlockHashRequestModel
    {
        public string height { get; set; }
    }
}
