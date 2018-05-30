using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Features.Consensus.Models
{
    public class GetBlockHashRequestModel
    {
        public string height { get; set; }
    }
}
