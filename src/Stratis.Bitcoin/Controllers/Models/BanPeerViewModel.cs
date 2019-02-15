using System;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// Represents the model that will ban and disconnect a connected peer.
    /// </summary>
    public sealed class BanPeerViewModel
    {
        public BanPeerViewModel()
        {
            this.BanDurationSeconds = TimeSpan.FromDays(1).Seconds;
        }

        /// <summary> The IP address of the connected peer to ban.</summary>
        public string PeerAddress { get; set; }

        /// <summary>
        /// The duration in seconds the peer will be banned.
        /// </summary>
        public int BanDurationSeconds { get; set; }
    }
}
