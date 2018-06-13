﻿using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Configuration of mempool features and limits.
    /// </summary>
    public class MempoolSettings
    {
        // Default value for "whitelistrelay" option.
        /// <seealso cref="WhiteListRelay"/>
        private const bool DefaultWhiteListRelay = true;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Maximal size of the transaction memory pool in megabytes.</summary>
        public int MaxMempool { get; set; }

        /// <summary>Maximum number of hours to keep transactions in the mempool.</summary>
        public int MempoolExpiry { get; set; }

        /// <summary><c>true</c> to require high priority for relaying free or low-fee transactions.</summary>
        public bool RelayPriority { get; set; }

        /// <summary>Number of kB/minute at which free transactions (with enough priority) will be accepted.</summary>
        public int LimitFreeRelay { get; set; }

        /// <summary>Maximum number of ancestors of a transaction in mempool (including itself).</summary>
        public int LimitAncestors { get; set; }

        /// <summary>Maximal size in kB of ancestors of a transaction in mempool (including itself).</summary>
        public int LimitAncestorSize { get; set; }

        /// <summary>Maximum number of descendants any ancestor can have in mempool (including itself).</summary>
        public int LimitDescendants { get; set; }

        /// <summary>Maximum size in kB of descendants any ancestor can have in mempool (including itself).</summary>
        public int LimitDescendantSize { get; set; }

        /// <summary><c>true</c> to enable transaction replacement in the memory pool.</summary>
        public bool EnableReplacement { get; set; }

        /// <summary>Maximum number of orphan transactions kept in memory.</summary>
        public int MaxOrphanTx { get; set; }

        /// <summary><c>true</c> to accept relayed transactions received from whitelisted peers even when not relaying transactions.</summary>
        public bool WhiteListRelay { get; set; }

        /// <summary>Option to skip (most) non-standard transaction checks, for testnet/regtest only.</summary>
        public bool RequireStandard { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the default configuration.
        /// </summary>
        public MempoolSettings() : this(NodeSettings.Default())
        {
        }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public MempoolSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(MempoolSettings).FullName);
            this.logger.LogTrace("({0}:'{1}')", nameof(nodeSettings), nodeSettings.Network.Name);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.MaxMempool = config.GetOrDefault("maxmempool", MempoolValidator.DefaultMaxMempoolSize);
            this.logger.LogDebug("MaxMempool set to {0}.", this.MaxMempool);

            this.MempoolExpiry = config.GetOrDefault("mempoolexpiry", MempoolValidator.DefaultMempoolExpiry);
            this.logger.LogDebug("MempoolExpiry set to {0}.", this.MempoolExpiry);

            this.RelayPriority = config.GetOrDefault("relaypriority", MempoolValidator.DefaultRelaypriority);
            this.logger.LogDebug("RelayPriority set to {0}.", this.RelayPriority);

            this.LimitFreeRelay = config.GetOrDefault("limitfreerelay", MempoolValidator.DefaultLimitfreerelay);
            this.logger.LogDebug("LimitFreeRelay set to {0}.", this.LimitFreeRelay);

            this.LimitAncestors = config.GetOrDefault("limitancestorcount", MempoolValidator.DefaultAncestorLimit);
            this.logger.LogDebug("LimitAncestors set to {0}.", this.LimitAncestors);

            this.LimitAncestorSize = config.GetOrDefault("limitancestorsize", MempoolValidator.DefaultAncestorSizeLimit);
            this.logger.LogDebug("LimitAncestorSize set to {0}.", this.LimitAncestorSize);

            this.LimitDescendants = config.GetOrDefault("limitdescendantcount", MempoolValidator.DefaultDescendantLimit);
            this.logger.LogDebug("LimitDescendants set to {0}.", this.LimitDescendants);

            this.LimitDescendantSize = config.GetOrDefault("limitdescendantsize", MempoolValidator.DefaultDescendantSizeLimit);
            this.logger.LogDebug("LimitDescendantSize set to {0}.", this.LimitDescendantSize);

            this.EnableReplacement = config.GetOrDefault("mempoolreplacement", MempoolValidator.DefaultEnableReplacement);
            this.logger.LogDebug("EnableReplacement set to {0}.", this.EnableReplacement);

            this.MaxOrphanTx = config.GetOrDefault("maxorphantx", MempoolOrphans.DefaultMaxOrphanTransactions);
            this.logger.LogDebug("MaxOrphanTx set to {0}.", this.MaxOrphanTx);

            this.WhiteListRelay = config.GetOrDefault("whitelistrelay", DefaultWhiteListRelay);
            this.logger.LogDebug("WhiteListRelay set to {0}.", this.WhiteListRelay);

            this.RequireStandard = config.GetOrDefault("acceptnonstdtxn", !(nodeSettings.Network.IsTest()));
            this.logger.LogDebug("RequireStandard set to {0}.", this.RequireStandard);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Prints the help information on how to configure the mempool settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-maxmempool=<megabytes>   Maximal size of the transaction memory pool in megabytes. Defaults to { MempoolValidator.DefaultMaxMempoolSize }.");
            builder.AppendLine($"-mempoolexpiry=<hours>    Maximum number of hours to keep transactions in the mempool. Defaults to { MempoolValidator.DefaultMempoolExpiry }.");
            builder.AppendLine($"-relaypriority=<0 or 1>   Enable high priority for relaying free or low-fee transactions.");
            builder.AppendLine($"-limitfreerelay=<kB/minute>  Number of kB/minute at which free transactions (with enough priority) will be accepted. Defaults to { MempoolValidator.DefaultLimitfreerelay }.");
            builder.AppendLine($"-limitancestorcount=<count>  Maximum number of ancestors of a transaction in mempool (including itself). Defaults to { MempoolValidator.DefaultAncestorLimit }.");
            builder.AppendLine($"-limitancestorsize=<kB>   Maximal size in kB of ancestors of a transaction in mempool (including itself). Defaults to { MempoolValidator.DefaultAncestorSizeLimit }.");
            builder.AppendLine($"-limitdescendantcount=<count>  Maximum number of descendants any ancestor can have in mempool (including itself). Defaults to { MempoolValidator.DefaultDescendantLimit }.");
            builder.AppendLine($"-limitdescendantsize=<kB> Maximum size in kB of descendants any ancestor can have in mempool (including itself). Defaults to { MempoolValidator.DefaultDescendantSizeLimit }.");
            builder.AppendLine($"-mempoolreplacement=<0 or 1>  Enable transaction replacement in the memory pool.");
            builder.AppendLine($"-maxorphantx=<kB>         Maximum number of orphan transactions kept in memory. Defaults to { MempoolOrphans.DefaultMaxOrphanTransactions }.");
            builder.AppendLine($"-whitelistrelay=<0 or 1>  Enable to accept relayed transactions received from whitelisted peers even when not relaying transactions. Defaults to { DefaultWhiteListRelay }.");
            builder.AppendLine($"-acceptnonstdtxn=<0 or 1> Accept non-standard transactions. Default {(!(network.IsTest())?1:0)}.");

            NodeSettings.Default().Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####MemPool Settings####");
            builder.AppendLine($"#Maximal size of the transaction memory pool in megabytes. Defaults to { MempoolValidator.DefaultMaxMempoolSize }.");
            builder.AppendLine($"#maxmempool={ MempoolValidator.DefaultMaxMempoolSize }");
            builder.AppendLine($"#Maximum number of hours to keep transactions in the mempool. Defaults to { MempoolValidator.DefaultMempoolExpiry }.");
            builder.AppendLine($"#mempoolexpiry={ MempoolValidator.DefaultMempoolExpiry }");
            builder.AppendLine($"#Enable high priority for relaying free or low-fee transactions. Defaults to { (MempoolValidator.DefaultRelaypriority?1:0) }.");
            builder.AppendLine($"#relaypriority={ (MempoolValidator.DefaultRelaypriority?1:0) }");
            builder.AppendLine($"#Number of kB/minute at which free transactions (with enough priority) will be accepted. Defaults to { MempoolValidator.DefaultLimitfreerelay }.");
            builder.AppendLine($"#limitfreerelay={ MempoolValidator.DefaultLimitfreerelay }");
            builder.AppendLine($"#Maximum number of ancestors of a transaction in mempool (including itself). Defaults to { MempoolValidator.DefaultAncestorLimit }.");
            builder.AppendLine($"#limitancestorcount={ MempoolValidator.DefaultAncestorLimit }");
            builder.AppendLine($"#Maximal size in kB of ancestors of a transaction in mempool (including itself). Defaults to { MempoolValidator.DefaultAncestorSizeLimit }.");
            builder.AppendLine($"#limitancestorsize={ MempoolValidator.DefaultAncestorSizeLimit }");
            builder.AppendLine($"#Maximum number of descendants any ancestor can have in mempool (including itself). Defaults to { MempoolValidator.DefaultDescendantLimit }.");
            builder.AppendLine($"#limitdescendantcount={ MempoolValidator.DefaultDescendantLimit }");
            builder.AppendLine($"#Maximum size in kB of descendants any ancestor can have in mempool (including itself). Defaults to { MempoolValidator.DefaultDescendantSizeLimit }.");
            builder.AppendLine($"#limitdescendantsize={ MempoolValidator.DefaultDescendantSizeLimit }.");
            builder.AppendLine($"#Enable transaction replacement in the memory pool.");
            builder.AppendLine($"#mempoolreplacement=0");
            builder.AppendLine($"#Maximum number of orphan transactions kept in memory. Defaults to { MempoolOrphans.DefaultMaxOrphanTransactions }.");
            builder.AppendLine($"#maxorphantx={ MempoolOrphans.DefaultMaxOrphanTransactions }");
            builder.AppendLine($"#Enable to accept relayed transactions received from whitelisted peers even when not relaying transactions. Defaults to { (DefaultWhiteListRelay?1:0) }.");
            builder.AppendLine($"#whitelistrelay={ (DefaultWhiteListRelay?1:0) }");
            builder.AppendLine($"#Accept non-standard transactions. Default {(!(network.IsTest())?1:0)}.");
            builder.AppendLine($"#acceptnonstdtxn={(!(network.IsTest())?1:0)}");
        }
    }
}
