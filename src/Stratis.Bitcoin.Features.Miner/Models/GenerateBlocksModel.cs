using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Miner.Models
{
    /// <summary>
    /// Represents a list of blocks generated through mining, as an API return object.
    /// </summary>
    public class GenerateBlocksModel
    {
        /// <summary>
        /// The list of blocks mined.
        /// </summary>
        [JsonProperty(PropertyName = "blocks")]
        public IList<uint256> Blocks { get; set; }
    }
}
