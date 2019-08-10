using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Features.FederatedPeg.Models
{
    public interface IMaturedBlocksRequestModel
    {
        int MaxBlocksToSend { get; set; }
        int MaxDepositsToSend { get; set; }
        int BlockHeight { get; set; }
    }

    /// <summary>
    /// This is used when requesting blocks from chain A.
    /// </summary>
    public class MaturedBlockRequestModel : RequestModel, IMaturedBlocksRequestModel
    {
        public MaturedBlockRequestModel(int blockHeight, int maxBlocksToSend, int maxDepositsToSend = int.MaxValue)
        {
            this.BlockHeight = blockHeight;
            this.MaxBlocksToSend = maxBlocksToSend;
            this.MaxDepositsToSend = maxDepositsToSend;
        }

        [Required(ErrorMessage = "The maximum number of blocks to fetch is required")]
        public int MaxBlocksToSend { get; set; }

        [Required(ErrorMessage = "The maximum number of deposits to fetch is required")]
        public int MaxDepositsToSend { get; set; }

        [Required(ErrorMessage = "The block height is required")]
        public int BlockHeight { get; set; }
    }
}
