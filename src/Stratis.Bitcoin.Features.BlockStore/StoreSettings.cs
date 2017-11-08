using System;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// Configuration related to storage of transactions.
    /// </summary>
    public class StoreSettings
    {
        /// <summary><c>true</c> to maintain a full transaction index.</summary>
        public bool TxIndex { get; set; }

        /// <summary><c>true</c> to rebuild chain state and block index from block data files on disk.</summary>
        public bool ReIndex { get; set; }

        /// <summary><c>true</c> to enable pruning to reduce storage requirements by enabling deleting of old blocks.</summary>
        public bool Prune { get; set; }

        private Action<StoreSettings> callback = null;

        public StoreSettings()
        {
        }

        public StoreSettings(Action<StoreSettings> callback)
            :this()
        {
            this.callback = callback;
        }

        public StoreSettings(NodeSettings nodeSettings, Action<StoreSettings> callback = null)
            :this(callback)
        {
            this.Load(nodeSettings);
        }

        /// <summary>
        /// Loads the storage related settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public virtual void Load(NodeSettings nodeSettings)
        {
            var config = nodeSettings.ConfigReader;

            this.Prune = config.GetOrDefault("prune", 0) != 0;
            this.TxIndex = config.GetOrDefault("txindex", 0) != 0;
            this.ReIndex = config.GetOrDefault("reindex", 0) != 0;

            this.callback?.Invoke(this);

            if (this.Prune && this.TxIndex)
                throw new ConfigurationException("Prune mode is incompatible with -txindex");

            // TODO: --reindex

        }
    }
}