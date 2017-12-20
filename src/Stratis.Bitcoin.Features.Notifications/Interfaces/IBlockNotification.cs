using NBitcoin;

namespace Stratis.Bitcoin.Features.Notifications.Interfaces
{
    public interface IBlockNotification
    {
        /// <summary>
        /// Notifies about blocks, starting from block with hash passed as parameter.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops block notification by waiting for the async loop to complete.
        /// </summary>
        void Stop();

        /// <summary>
        /// Looks up and sets the start hash to sync from.
        /// </summary>
        /// <param name="startHash">The hash to start syncing from.</param>
        void SyncFrom(uint256 startHash);
    }
}
