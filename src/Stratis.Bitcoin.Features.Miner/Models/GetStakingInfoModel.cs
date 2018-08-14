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
        [JsonProperty(PropertyName = "currentBlockSize")]
        public long CurrentBlockSize { get; set; }

        /// <summary>Number of transactions the node wants to put in the next block.</summary>
        [JsonProperty(PropertyName = "currentBlockTx")]
        public long CurrentBlockTx { get; set; }

        /// <summary>Number of transactions in the memory pool.</summary>
        [JsonProperty(PropertyName = "pooledTx")]
        public long PooledTx { get; set; }

        /// <summary>Target difficulty that the next block must meet.</summary>
        [JsonProperty(PropertyName = "difficulty")]
        public double Difficulty { get; set; }

        /// <summary>Length of the last staking search interval in seconds.</summary>
        [JsonProperty(PropertyName = "searchInterval")]
        public int SearchInterval { get; set; }

        /// <summary>Staking weight of the node.</summary>
        [JsonProperty(PropertyName = "weight")]
        public long Weight { get; set; }

        /// <summary>Estimation of the total staking weight of all nodes on the network.</summary>
        [JsonProperty(PropertyName = "netStakeWeight")]
        public long NetStakeWeight { get; set; }

        /// <summary>Expected time of the node to find new block in seconds.</summary>
        [JsonProperty(PropertyName = "expectedTime")]
        public long ExpectedTime { get; set; }

        /// <inheritdoc />
        public object Clone()
        {
            var res = new GetStakingInfoModel
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

        /// <summary>
        /// Reset Weight and ExpectedTime values and set Staking to false.
        /// </summary>
        public void PauseStaking()
        {
            this.Staking = false;
            this.Weight = 0;
            this.ExpectedTime = 0;
        }

        /// <summary>
        /// Resume staking. Set staking to true.
        /// </summary>
        /// <param name="weight">Weight</param>
        /// <param name="expectedTime">Expected time</param>
        public void ResumeStaking(long weight, long expectedTime)
        {
            this.Staking = true;
            this.Weight = weight;
            this.ExpectedTime = expectedTime;
            this.Errors = null;
        }

        /// <summary>
        /// Reset all values to default.
        /// </summary>
        public void StopStaking()
        {
            this.Enabled = false;
            this.Staking = false;
            this.Errors = null;
            this.CurrentBlockSize = 0;
            this.CurrentBlockTx = 0;
            this.PooledTx = 0;
            this.Difficulty = 0;
            this.SearchInterval = 0;
            this.Weight = 0;
            this.NetStakeWeight = 0;
            this.ExpectedTime = 0;
        }
    }
}
