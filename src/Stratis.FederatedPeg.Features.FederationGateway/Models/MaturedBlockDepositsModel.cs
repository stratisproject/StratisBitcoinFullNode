using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// When a block matures, an instance of this class is created and passed on to the target chain.
    /// If there are no deposits, we still need to send an empty list with corresponding block (height
    /// and hash) so that the target node knows that block has been seen and dealt with.
    /// </summary>
    public class MaturedBlockDepositsModel : RequestModel
    {
        public MaturedBlockDepositsModel(MaturedBlockInfoModel maturedBlockInfo, IReadOnlyList<IDeposit> deposits)
        {
            this.BlockInfo = maturedBlockInfo;
            this.Deposits = deposits;
        }

        [Required(ErrorMessage = "A list of deposits is required")]
        [JsonConverter(typeof(ConcreteConverter<List<Deposit>>))]
        public IReadOnlyList<IDeposit> Deposits { get; set; }

        [Required(ErrorMessage = "A block is required")]
        [JsonConverter(typeof(ConcreteConverter<MaturedBlockInfoModel>))]
        public IMaturedBlockInfo BlockInfo { get; set; }
    }
}
