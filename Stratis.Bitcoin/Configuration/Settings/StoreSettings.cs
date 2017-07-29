﻿namespace Stratis.Bitcoin.Configuration.Settings
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

        /// <summary>
        /// Loads the storage related settings from the application configuration.
        /// </summary>
        /// <param name="config">Application configuration.</param>
        public void Load(TextFileConfiguration config)
        {
            this.Prune = config.GetOrDefault("prune", 0) != 0;
            this.TxIndex = config.GetOrDefault("txindex", 0) != 0;
            if (this.Prune && this.TxIndex)
                throw new ConfigurationException("Prune mode is incompatible with -txindex");

            this.ReIndex = config.GetOrDefault("reindex", 0) != 0;

            // TODO: --reindex
        }
    }
}