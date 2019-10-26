namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// Represents the model that will disconnect a connected peer.
    /// </summary>
    public sealed class DisconnectPeerViewModel
    {
        /// <summary> The IP address and port of the connected peer to disconnect.</summary>
        public string PeerAddress { get; set; }
    }
}