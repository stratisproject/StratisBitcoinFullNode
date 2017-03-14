using NBitcoin;

namespace Stratis.Bitcoin.Notifications
{
    /// <summary>
    /// Holds a record of the hash from which the broadcasting of blocks will start.
    /// </summary>
    public class BlockNotificationStartHash
    {
        /// <summary>
        /// The hash from which the notification will start.
        /// </summary>
        public uint256 StartHash { get; set; }

        public BlockNotificationStartHash(uint256 startHash)
        {
            this.StartHash = startHash;
        }                
    }
}
