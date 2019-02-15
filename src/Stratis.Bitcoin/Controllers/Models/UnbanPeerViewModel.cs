namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary> Represents the model that will un-ban a banned peer.</summary>
    public sealed class UnBanPeerViewModel
    {
        /// <summary> The IP address of the banned peer.</summary>
        public string PeerAddress { get; set; }
    }
}
