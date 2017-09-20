using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.IndexStore
{
    /// <summary>
    /// Configuration related to storage of transactions.
    /// </summary>
    public class IndexSettings:StoreSettings
    {
        private Action<IndexSettings> callback = null;
        public Dictionary<string, IndexExpression> indexes { get; private set; }

        public IndexSettings()
            :base()
        {
            this.indexes = new Dictionary<string, IndexExpression>();
        }

        public IndexSettings(Action<IndexSettings> callback)
            :this()
        {
            this.callback = callback;
        }

        public void RegisterIndex(string name, string builder, bool multiValue, string[] dependencies = null)
        {
            this.indexes[name] = new IndexExpression(multiValue, builder, dependencies);
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