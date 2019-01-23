using System;

namespace Stratis.Bitcoin.Base.Deployments.Models
{
    /// <summary>
    /// Class representing the activation states with the count of blocks in each state.
    /// </summary>
    public class ThresholdStateModel
    {
        public int DeploymentIndex { get; }

        public ThresholdState? StateValue { get; set; }

        public string ThresholdState { get; set; }

        public int Height { get; }

        public int PeriodStartsHeight { get; }

        public int PeriodEndsHeight { get; }

        public int Votes { get; set; }

        public int Threshold { get; }

        public DateTime? TimeStart { get; }

        public DateTime? TimeTimeOut { get; }

        public ThresholdStateModel(int deploymentIndex, int votes, DateTime? timeStart, DateTime? timeTimeOut, int threshold, int height, int periodStartsHeight, int periodEndsHeight, ThresholdState stateValue, string thresholdState)
        {
            this.DeploymentIndex = deploymentIndex;
            this.Votes = votes;
            this.TimeStart = timeStart;
            this.TimeTimeOut = timeTimeOut;
            this.Threshold = threshold;
            this.Height = height;
            this.PeriodStartsHeight = periodStartsHeight;
            this.PeriodEndsHeight = periodEndsHeight;
            this.StateValue = stateValue;
            this.ThresholdState = thresholdState;
        }

        public ThresholdStateModel() { }
    }
}