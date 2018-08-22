using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus.ValidationResults
{
    /// <summary>
    /// Information related to the block full validation process.
    /// </summary>
    internal class ConnectBlocksResult : ValidationResult
    {
        public bool ConsensusTipChanged { get; set; }

        /// <summary>List of peer IDs to be banned and disconnected.</summary>
        /// <remarks><c>null</c> in case <see cref="ValidationResult.Succeeded"/> is <c>false</c>.</remarks>
        public List<int> PeersToBan { get; set; }

        public ChainedHeader LastValidatedBlockHeader { get; set; }

        public ConnectBlocksResult(bool succeeded)
        {
            this.Succeeded = succeeded;
        }

        public override string ToString()
        {
            if (this.Succeeded)
                return $"{nameof(this.Succeeded)}={this.Succeeded}";

            return $"{nameof(this.Succeeded)}={this.Succeeded},{nameof(this.ConsensusTipChanged)}={this.ConsensusTipChanged},{nameof(this.PeersToBan)}.{nameof(this.PeersToBan.Count)}={this.PeersToBan.Count},{nameof(this.BanReason)}={this.BanReason},{nameof(this.BanDurationSeconds)}={this.BanDurationSeconds}";
        }
    }
}
