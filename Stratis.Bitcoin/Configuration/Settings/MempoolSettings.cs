using Stratis.Bitcoin.MemoryPool;

namespace Stratis.Bitcoin.Configuration.Settings
{
	public class MempoolSettings
	{
		// Default for blocks only 
		const bool DEFAULT_BLOCKSONLY = false;
		// Default for DEFAULT_WHITELISTRELAY. 
		const bool DEFAULT_WHITELISTRELAY = true;

		public int MaxMempool { get; set; }
		public int MempoolExpiry { get; set; }
		public bool RelayPriority { get; set; }
		public int LimitFreeRelay { get; set; }
		public int LimitAncestors { get; set; }
		public int LimitAncestorSize { get; set; }
		public int LimitDescendants { get; set; }
		public int LimitDescendantSize { get; set; }
		public bool EnableReplacement { get; set; }
		public int MaxOrphanTx { get; set; }
		public bool RelayTxes { get; set; }
		public bool Whitelistrelay { get; set; }

		public void Load(TextFileConfiguration config)
		{
			this.MaxMempool = config.GetOrDefault("maxmempool", MempoolValidator.DefaultMaxMempoolSize);
			this.MempoolExpiry = config.GetOrDefault("mempoolexpiry", MempoolValidator.DefaultMempoolExpiry);
			this.RelayPriority = config.GetOrDefault("relaypriority", MempoolValidator.DefaultRelaypriority);
			this.LimitFreeRelay = config.GetOrDefault("limitfreerelay", MempoolValidator.DefaultLimitfreerelay);
			this.LimitAncestors = config.GetOrDefault("limitancestorcount", MempoolValidator.DefaultAncestorLimit);
			this.LimitAncestorSize = config.GetOrDefault("limitancestorsize", MempoolValidator.DefaultAncestorSizeLimit);
			this.LimitDescendants = config.GetOrDefault("limitdescendantcount", MempoolValidator.DefaultDescendantLimit);
			this.LimitDescendantSize = config.GetOrDefault("limitdescendantsize", MempoolValidator.DefaultDescendantSizeLimit);
			this.EnableReplacement = config.GetOrDefault("mempoolreplacement", MempoolValidator.DefaultEnableReplacement);
			this.MaxOrphanTx = config.GetOrDefault("maxorphantx", MempoolOrphans.DEFAULT_MAX_ORPHAN_TRANSACTIONS);
			this.RelayTxes = !config.GetOrDefault("blocksonly", DEFAULT_BLOCKSONLY);
			this.Whitelistrelay = config.GetOrDefault("whitelistrelay", DEFAULT_WHITELISTRELAY);
		}
	}
}