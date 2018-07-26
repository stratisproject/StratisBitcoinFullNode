using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// Configuration related to the miner interface.
    /// </summary>
    public class MinerSettings
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Enable the node to stake.
        /// </summary>
        public bool Stake { get; private set; }

        /// <summary>
        /// Enable the node to mine.
        /// </summary>
        public bool Mine { get; private set; }

        /// <summary>
        /// An address to use when mining, if not specified and address from the wallet will be used.
        /// </summary>
        public string MineAddress { get; set; }

        /// <summary>
        /// The wallet password needed when staking to sign blocks.
        /// </summary>
        public string WalletPassword { get; set; }

        /// <summary>
        /// The wallet name to select outputs to stake.
        /// </summary>
        public string WalletName { get; set; }

        /// <summary>
        /// Settings for <see cref="BlockDefinition"/>.
        /// </summary>
        public BlockDefinitionOptions BlockDefinitionOptions { get; }

        /// <summary>
        /// Initializes an instance of the object from the default configuration.
        /// </summary>
        public MinerSettings() : this(NodeSettings.Default())
        {
        }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public MinerSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(MinerSettings).FullName);
            this.logger.LogTrace("({0}:'{1}')", nameof(nodeSettings), nodeSettings.Network.Name);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.Mine = config.GetOrDefault<bool>("mine", false, this.logger);
            if (this.Mine)
                this.MineAddress = config.GetOrDefault<string>("mineaddress", null, this.logger);

            this.Stake = config.GetOrDefault<bool>("stake", false, this.logger);
            if (this.Stake)
            {
                this.WalletName = config.GetOrDefault<string>("walletname", null, this.logger);
                this.WalletPassword = config.GetOrDefault<string>("walletpassword", null); // No logging!
            }

            uint blockMaxSize = (uint) config.GetOrDefault<int>("blockmaxsize", (int) nodeSettings.Network.Consensus.Options.MaxBlockSerializedSize, this.logger);
            uint blockMaxWeight = (uint) config.GetOrDefault<int>("blockmaxweight", (int) nodeSettings.Network.Consensus.Options.MaxBlockWeight, this.logger);

            this.BlockDefinitionOptions = new BlockDefinitionOptions(blockMaxWeight, blockMaxSize).RestrictForNetwork(nodeSettings.Network);

            this.logger.LogTrace("(-)");
        }
        
        /// <summary>
        /// Displays mining help information on the console.
        /// </summary>
        /// <param name="mainNet">Not used.</param>
        public static void PrintHelp(Network mainNet)
        {
            NodeSettings defaults = NodeSettings.Default();
            var builder = new StringBuilder();

            builder.AppendLine("-mine=<0 or 1>            Enable POW mining.");
            builder.AppendLine("-stake=<0 or 1>           Enable POS.");
            builder.AppendLine("-mineaddress=<string>     The address to use for mining (empty string to select an address from the wallet).");
            builder.AppendLine("-walletname=<string>      The wallet name to use when staking.");
            builder.AppendLine("-walletpassword=<string>  Password to unlock the wallet.");
            builder.AppendLine("-blockmaxsize=<number>    Maximum block size (in bytes) for the miner to generate.");
            builder.AppendLine("-blockmaxweight=<number>  Maximum block weight (in weight units) for the miner to generate.");

            defaults.Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Miner Settings####");
            builder.AppendLine("#Enable POW mining.");
            builder.AppendLine("#mine=0");
            builder.AppendLine("#Enable POS.");
            builder.AppendLine("#stake=0");
            builder.AppendLine("#The address to use for mining (empty string to select an address from the wallet).");
            builder.AppendLine("#mineaddress=<string>");
            builder.AppendLine("#The wallet name to use when staking.");
            builder.AppendLine("#walletname=<string>");
            builder.AppendLine("#Password to unlock the wallet.");
            builder.AppendLine("#walletpassword=<string>");
            builder.AppendLine("#Maximum block size (in bytes) for the miner to generate.");
            builder.AppendLine($"#blockmaxsize={network.Consensus.Options.MaxBlockSerializedSize}");
            builder.AppendLine("#Maximum block weight (in weight units) for the miner to generate.");
            builder.AppendLine($"#blockmaxweight={network.Consensus.Options.MaxBlockWeight}");
        }
    }
}
