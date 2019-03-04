using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Base.Deployments.Models
{
    /// <summary>
    /// Class representing information about the current activation state of a deployment.
    /// </summary>
    public class ThresholdStateModel
    {
        /// <summary>
        /// BIP9 deployment index for this soft fork.
        /// </summary>
        [JsonProperty(PropertyName = "deploymentIndex")]
        public int DeploymentIndex { get; set; }

        /// <summary>
        /// Activation state of this deployment at this block height.
        /// </summary>
        [JsonProperty(PropertyName = "stateValue")]
        public ThresholdState? StateValue { get; set; }

        /// <summary>
        /// Readable name of threshold state.
        /// </summary>
        [JsonProperty(PropertyName = "thresholdState")]
        public string ThresholdState { get; set; }

        /// <summary>
        /// Height of the the block with this threshold state.
        /// </summary>
        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        /// <summary>
        /// The number of blocks in the each confirmation window.
        /// </summary>
        [JsonProperty(PropertyName = "confirmationPeriod")]
        public int ConfirmationPeriod { get; set; }

        /// <summary>
        /// Height at start of activation window.
        /// </summary>
        [JsonProperty(PropertyName = "periodStartHeight")]
        public int PeriodStartHeight { get; set; }

        /// <summary>
        /// Height at end of activation window.
        /// </summary>
        [JsonProperty(PropertyName = "periodEndHeight")]
        public int PeriodEndHeight { get; set; }

        /// <summary>
        /// Number of blocks with flags set for this BIP9 deployment in the last confirmation window.
        /// </summary>
        [JsonProperty(PropertyName = "votes")]
        public int Votes { get; set; }

        /// <summary>
        /// The total number of blocks in the last confirmation window.
        /// </summary>
        [JsonProperty(PropertyName = "blocks")]
        public int Blocks { get; set; }

        /// <summary>
        /// A summary of block version counts in the last confirmation window.
        /// </summary>
        [JsonProperty(PropertyName = "versions")]
        public Dictionary<string, int> HexVersions { get; set; }

        /// <summary>
        /// Activation vote threshold for this BIP9 deployment.
        /// </summary>
        [JsonProperty(PropertyName = "threshold")]
        public int Threshold { get; set; }

        /// <summary>
        /// Start time for vote counting for this BIP9 deployment.
        /// </summary>
        [JsonProperty(PropertyName = "timeStart")]
        public DateTime? TimeStart { get; set; }

        /// <summary>
        /// End time for vote counting for this BIP9 deployment.
        /// </summary>
        [JsonProperty(PropertyName = "timeTimeOut")]
        public DateTime? TimeTimeOut { get; set; }
    }
}