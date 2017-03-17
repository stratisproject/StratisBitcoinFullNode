namespace Stratis.Bitcoin.Configuration.Settings
{
	public class StoreSettings
	{
		public bool TxIndex { get; set; }
		public bool ReIndex { get; set; }
		public bool Prune { get; set; }

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