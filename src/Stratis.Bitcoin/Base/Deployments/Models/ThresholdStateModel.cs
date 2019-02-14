using System;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Base.Deployments.Models
{
    /// <summary>
    /// Class representing the activation states with the count of blocks in each state.
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
        /// Height at start of activation window.
        /// </summary>
        [JsonProperty(PropertyName = "periodStartsHeight")]
        public int PeriodStartsHeight { get; set; }

        /// <summary>
        /// Height at end of activation window.
        /// </summary>
        [JsonProperty(PropertyName = "periodEndsHeight")]
        public int PeriodEndsHeight { get; set; }

        /// <summary>
        /// Activation votes for this BIP9 deployment.
        /// </summary>
        [JsonProperty(PropertyName = "votes")]
        public int Votes { get; set; }

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