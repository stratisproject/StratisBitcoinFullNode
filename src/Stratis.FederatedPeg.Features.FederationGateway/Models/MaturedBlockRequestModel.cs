using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// This is used when requesting blocks from chain A.
    /// </summary>
    public class MaturedBlockRequestModel : RequestModel, IMaturedBlocksRequestModel
    {
        public MaturedBlockRequestModel(int blockHeight, int maxBlocksToSend = int.MaxValue)
        {
            this.BlockHeight = blockHeight;
            this.MaxBlocksToSend = maxBlocksToSend;
        }

        [Required(ErrorMessage = "The maximum number of blocks to fetch is required")]
        public int MaxBlocksToSend { get; set; }

        [Required(ErrorMessage = "The block height is required")]
        public int BlockHeight { get; set; }
    }
}
