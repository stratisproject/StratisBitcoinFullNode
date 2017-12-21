namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Defines the interface to manage the whitelist.
    /// </summary>
    public interface IWhitelistManager
    {
        /// <summary>
        /// Refreshes the managed whitelist.
        /// </summary>
        void RefreshWhitelist();
    }
}