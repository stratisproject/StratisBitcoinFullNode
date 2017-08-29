namespace Stratis.Bitcoin.Configuration.Settings
{
    /// <summary>
    /// Configuration of cache limits.
    /// </summary>
    public class CacheSettings
    {
        /// <summary>Maximum number of items in <see cref="Features.Consensus.CoinViews.CachedCoinView"/>.</summary>
        public int MaxItems { get; set; } 

        /// <summary>
        /// Initializes properties with default values.
        /// </summary>
        public CacheSettings()
        {
            this.MaxItems = Features.Consensus.CoinViews.CachedCoinView.CacheMaxItemsDefault;
        }
    }
}