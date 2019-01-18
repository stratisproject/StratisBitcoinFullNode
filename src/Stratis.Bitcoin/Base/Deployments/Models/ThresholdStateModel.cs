using System;

namespace Stratis.Bitcoin.Base.Deployments.Models
{
    /// <summary>
    /// Class representing the activation states with the count of blocks in each state.
    /// </summary>
    public class ThresholdStateModel
    {
        public int DeploymentIndex { get; }

        public int BlocksDefined { get; }

        public int BlocksStarted { get; }

        public int BlocksLockedIn { get; }

        public int BlocksFailed { get; }

        public int BlocksActive { get; }

        public DateTimeOffset? TimePast { get; set; }

        public DateTimeOffset? TimeStart { get; }

        public DateTimeOffset? TimeTimeOut { get; }

        public int Threshold { get; }

        public int Votes { get; set; }

        public ThresholdState? StateValue { get; set; }

        public string ThresholdState { get; set; }

        public ThresholdStateModel(int deploymentIndex, int blocksDefined, int blocksStarted, int blocksLockedIn,
            int blocksFailed, int blocksActive, DateTimeOffset? medianTimePast, DateTimeOffset? timeStart, DateTimeOffset? timeTimeOut, int threshold, int votes, ThresholdState stateValue, string thresholdState)
        {
            this.DeploymentIndex = deploymentIndex;
            this.BlocksDefined = blocksDefined;
            this.BlocksStarted = blocksStarted;
            this.BlocksLockedIn = blocksLockedIn;
            this.BlocksFailed = blocksFailed;
            this.BlocksActive = blocksActive;
            this.TimePast = medianTimePast;
            this.TimeStart = timeStart;
            this.TimeTimeOut = timeTimeOut;
            this.Votes = votes;
            this.Threshold = threshold;
            this.StateValue = stateValue;
            this.ThresholdState = thresholdState;
        }

        public ThresholdStateModel()
        {
        }
    }
}