using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus.ValidationResults
{
    /// <summary>
    /// Information related to the block full validation process.
    /// </summary>
    internal class ConnectBlocksResult : ValidationResult
    {
        public bool ConsensusTipChanged { get; private set; }

        /// <summary>List of peer IDs to be banned and disconnected.</summary>
        /// <remarks><c>null</c> in case <see cref="ValidationResult.Succeeded"/> is <c>false</c>.</remarks>
        public List<int> PeersToBan { get; private set; }

        public ChainedHeader LastValidatedBlockHeader { get; set; }

        private ConnectBlocksResult() { }

        public static ConnectBlocksResult Fail(bool consensusTipChanged = true)
        {
            var result = new ConnectBlocksResult
            {
                ConsensusTipChanged = consensusTipChanged,
                Succeeded = false
            };
            return result;
        }

        public static ConnectBlocksResult FailAndBanPeers(ConsensusError error, List<int> peersToBan, int banDurationSeconds)
        {
            var result = new ConnectBlocksResult
            {
                BanDurationSeconds = banDurationSeconds,
                BanReason = error.Message,
                ConsensusTipChanged = false,
                Error = error,
                PeersToBan = peersToBan,
                Succeeded = false
            };
            return result;
        }

        public static ConnectBlocksResult Success(bool consensusTipChanged = true)
        {
            var result = new ConnectBlocksResult
            {
                ConsensusTipChanged = consensusTipChanged,
                Succeeded = true
            };
            return result;
        }

        public override string ToString()
        {
            if (this.Succeeded)
                return $"{nameof(this.Succeeded)}={this.Succeeded}";

            return $"{nameof(this.Succeeded)}={this.Succeeded},{nameof(this.ConsensusTipChanged)}={this.ConsensusTipChanged},{nameof(this.PeersToBan)}.{nameof(this.PeersToBan.Count)}={this.PeersToBan.Count},{nameof(this.BanReason)}={this.BanReason},{nameof(this.BanDurationSeconds)}={this.BanDurationSeconds}";
        }
    }
}
