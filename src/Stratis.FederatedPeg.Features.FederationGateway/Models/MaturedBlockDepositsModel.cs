using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// When a block matures, an instance of this class is created and passed on to the target chain.
    /// If there are no deposits, we still need to send an empty list with corresponding block (height
    /// and hash) so that the target node knows that block has been seen and dealt with.
    /// </summary>
    public class MaturedBlockDepositsModel : RequestModel, IMaturedBlockDeposits
    {
        public MaturedBlockDepositsModel(MaturedBlockModel maturedBlock, IReadOnlyList<IDeposit> deposits)
        {
            this.Block = maturedBlock;
            this.Deposits = deposits;
        }

        [Required(ErrorMessage = "A list of deposits is required")]
        public IReadOnlyList<IDeposit> Deposits { get; set; }

        [Required(ErrorMessage = "A block is required")]
        public IMaturedBlock Block { get; set; }
    }
}
