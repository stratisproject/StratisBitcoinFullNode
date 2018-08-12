namespace Stratis.Bitcoin.Consensus.ValidationResults
{
    /// <summary>
    /// Feedback after a block was validated.
    /// </summary>
    public class ValidationResult
    {
        public int BanDurationSeconds { get; set; }

        public string BanReason { get; set; }

        public ConsensusError Error { get; set; }

        public bool Succeeded { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.Succeeded)
                return $"{nameof(this.Succeeded)}={this.Succeeded}";

            return $"{nameof(this.Succeeded)}={this.Succeeded},{nameof(this.BanReason)}={this.BanReason},{nameof(this.BanDurationSeconds)}={this.BanDurationSeconds}";
        }
    }
}