using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// An instance of this class represents a particular block hash and associated height on the source chain.
    /// </summary>
    public class MaturedBlockModel : RequestModel, IMaturedBlock
    {
        [Required(ErrorMessage = "A block hash is required")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        [Required(ErrorMessage = "A block height is required")]
        public int BlockHeight { get; set; }
    }
}
