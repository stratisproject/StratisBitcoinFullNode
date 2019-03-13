using System.ComponentModel.DataAnnotations;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Features.FederatedPeg.Models
{
    public interface IBlockTip
    {
        [JsonConverter(typeof(UInt256JsonConverter))]
        uint256 Hash { get; }

        int Height { get; }

        int MatureConfirmations { get; }
    }

    /// <summary>Block tip Hash, Height and MatureConfirmation model.</summary>
    public class BlockTipModel : RequestModel, IBlockTip
    {
        public BlockTipModel(uint256 hash, int height, int matureConfirmations)
        {
            this.Hash = hash;
            this.Height = height;
            this.MatureConfirmations = matureConfirmations;
        }

        [Required(ErrorMessage = "Block Hash is required")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Hash { get; set; }

        [Required(ErrorMessage = "Block Height is required")]
        public int Height { get; set; }

        [Required(ErrorMessage = "Mature Confirmations is required")]
        public int MatureConfirmations { get; set; }
    }
}
