using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// Information related to the block full validation process.
    /// </summary>
    public sealed class ConnectBlocksResult : ValidationResult
    {
        public bool ConsensusTipChanged { get; private set; }

        /// <summary>List of peer IDs to be banned and disconnected.</summary>
        /// <remarks><c>null</c> in case <see cref="ValidationResult.Succeeded"/> is <c>false</c>.</remarks>
        public List<int> PeersToBan { get; private set; }

        public ChainedHeader LastValidatedBlockHeader { get; set; }

        public ConnectBlocksResult(bool succeeded, bool consensusTipChanged = true, List<int> peersToBan = null, string banReason = null, int banDurationSeconds = 0)
        {
            this.ConsensusTipChanged = consensusTipChanged;
            this.Succeeded = succeeded;
            this.PeersToBan = peersToBan;
            this.BanReason = banReason;
            this.BanDurationSeconds = banDurationSeconds;
        }

        public override string ToString()
        {
            if (this.Succeeded)
                return $"{nameof(this.Succeeded)}={this.Succeeded}";

            return $"{nameof(this.Succeeded)}={this.Succeeded},{nameof(this.ConsensusTipChanged)}={this.ConsensusTipChanged},{nameof(this.PeersToBan)}.{nameof(this.PeersToBan.Count)}={this.PeersToBan.Count},{nameof(this.BanReason)}={this.BanReason},{nameof(this.BanDurationSeconds)}={this.BanDurationSeconds}";
        }
    }
}
