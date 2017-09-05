using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using System;

namespace Stratis.Bitcoin.Features.IndexStore
{
    /// <summary>
    /// Configuration related to storage of transactions.
    /// </summary>
    public class IndexSettings : StoreSettings
    {
        private Action<IndexSettings> callback = null;

        public IndexSettings()
            : base()
        {
        }

        public IndexSettings(Action<IndexSettings> callback)
            : this()
        {
            this.callback = callback;
        }

        /// <summary>
        /// Loads the storage related settings from the application configuration.
        /// </summary>
        /// <param name="config">Application configuration.</param>
        public override void Load(NodeSettings nodeSettings)
        {
            var config = nodeSettings.ConfigReader;

            this.Prune = false;
            this.TxIndex = true;

            this.callback?.Invoke(this);
        }
    }
}