using System;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Miner.Models
{
    /// <summary>
    /// Data structure returned by RPC command "getstakinginfo".
    /// </summary>
    public class GetStakingInfoModel : ICloneable
    {
        /// <summary><c>true</c> if the staking is enabled.</summary>
        [JsonProperty(PropertyName = "enabled")]
        public bool Enabled { get; set; }

        /// <summary><c>true</c> if the node is currently staking.</summary>
        [JsonProperty(PropertyName = "staking")]
        public bool Staking { get; set; }

        /// <summary>Last recorded warning message related to staking.</summary>
        [JsonProperty(PropertyName = "errors")]
        public string Errors { get; set; }

        /// <summary>Size of the next block the node wants to mine in bytes.</summary>
        [JsonProperty(PropertyName = "currentblocksize")]
        public long CurrentBlockSize { get; set; }

        /// <summary>Number of transactions the node wants to put in the next block.</summary>
        [JsonProperty(PropertyName = "currentblocktx")]
        public long CurrentBlockTx { get; set; }

        /// <summary>Number of transactions in the memory pool.</summary>
        [JsonProperty(PropertyName = "pooledtx")]
        public long PooledTx { get; set; }

        /// <summary>Target difficulty that the next block must meet.</summary>
        [JsonProperty(PropertyName = "difficulty")]
        public double Difficulty { get; set; }

        /// <summary>Length of the last staking search interval in seconds.</summary>
        [JsonProperty(PropertyName = "search-interval")]
        public int SearchInterval { get; set; }

        /// <summary>Staking weight of the node.</summary>
        [JsonProperty(PropertyName = "weight")]
        public long Weight { get; set; }

        /// <summary>Estimation of the total staking weight of all nodes on the network.</summary>
        [JsonProperty(PropertyName = "netstakeweight")]
        public long NetStakeWeight { get; set; }

        /// <summary>Expected time of the node to find new block in seconds.</summary>
        [JsonProperty(PropertyName = "expectedtime")]
        public long ExpectedTime { get; set; }

        /// <inheritdoc />
        public object Clone()
        {
            GetStakingInfoModel res = new GetStakingInfoModel
            {
                Enabled = this.Enabled,
                Staking = this.Staking,
                Errors = this.Errors,
                CurrentBlockSize = this.CurrentBlockSize,
                CurrentBlockTx = this.CurrentBlockTx,
                PooledTx = this.PooledTx,
                Difficulty = this.Difficulty,
                SearchInterval = this.SearchInterval,
                Weight = this.Weight,
                NetStakeWeight = this.NetStakeWeight,
                ExpectedTime = this.ExpectedTime
            };

            return res;
        }
    }
}
