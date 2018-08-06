namespace Stratis.Bitcoin.Consensus.ValidationResults
{
    public class ValidationResult
    {
        public bool Succeeded { get; set; }

        public int BanDurationSeconds { get; set; }

        public string BanReason { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.Succeeded)
                return $"{nameof(this.Succeeded)}={this.Succeeded}";

            return $"{nameof(this.Succeeded)}={this.Succeeded},{nameof(this.BanReason)}={this.BanReason},{nameof(this.BanDurationSeconds)}={this.BanDurationSeconds}";
        }
    }
}