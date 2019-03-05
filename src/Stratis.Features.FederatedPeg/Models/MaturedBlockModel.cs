using System.ComponentModel.DataAnnotations;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Features.FederatedPeg.Models
{
    public interface IMaturedBlockInfo
    {
        uint256 BlockHash { get; }

        int BlockHeight { get; }

        uint BlockTime { get; }
    }

    /// <summary>
    /// An instance of this class represents a particular block hash and associated height on the source chain.
    /// </summary>
    public class MaturedBlockInfoModel : RequestModel, IMaturedBlockInfo
    {
        [Required(ErrorMessage = "A block hash is required")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        [Required(ErrorMessage = "A block height is required")]
        public int BlockHeight { get; set; }

        [Required(ErrorMessage = "A block time is required")]
        public uint BlockTime { get; set; }
    }
}
