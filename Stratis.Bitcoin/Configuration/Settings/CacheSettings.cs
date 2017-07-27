namespace Stratis.Bitcoin.Configuration.Settings
{
    /// <summary>
    /// Configuration of cache limits.
    /// </summary>
    public class CacheSettings
    {
        /// <summary>
        /// Maximum number of items in <see cref="Features.Consensus.CoinViews.CachedCoinView"/>.
        /// </summary>
        public int MaxItems
        {
            get; set;
        } = 100000;
    }
}