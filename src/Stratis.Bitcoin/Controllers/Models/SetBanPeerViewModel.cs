namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// Represents the model that will ban and disconnect a connected peer.
    /// </summary>
    public sealed class SetBanPeerViewModel
    {
        /// <summary>
        /// Whether to add or remove the node from the banned list.
        /// <para>
        /// Options are "Add" or "Remove".
        /// </para>
        /// </summary>
        public string BanCommand { get; set; }

        /// <summary>
        /// The duration in seconds the peer will be banned.
        /// </summary>
        public int? BanDurationSeconds { get; set; }

        /// <summary>
        /// The IP address of the connected peer to ban.
        /// <para>
        /// The port should not be specified in this instance.
        /// </para>
        /// </summary>
        public string PeerAddress { get; set; }
    }
}
