namespace Stratis.Bitcoin.Consensus.ValidationResults
{
    /// <summary>
    /// Feedback after a block was validated.
    /// </summary>
    public class ValidationResult
    {
        public int BanDurationSeconds { get; internal set; }

        public string BanReason { get; internal set; }

        public ConsensusError Error { get; internal set; }

        public bool Succeeded { get; internal set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.Succeeded)
                return $"{nameof(this.Succeeded)}={this.Succeeded}";

            return $"{nameof(this.Succeeded)}={this.Succeeded},{nameof(this.BanReason)}={this.BanReason},{nameof(this.BanDurationSeconds)}={this.BanDurationSeconds}";
        }
    }
}