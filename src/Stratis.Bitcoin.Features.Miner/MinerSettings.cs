using System;
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
        /// The wallet name to select outputs ot stake.
        /// </summary>
        public string WalletName { get; set; }

        /// <summary>
        /// A callback allow changing the default settings.
        /// </summary>
        private readonly Action<MinerSettings> callback;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="callback">Callback routine to be called once the miner settings are loaded.</param>
        public MinerSettings(Action<MinerSettings> callback = null)
        {        
            this.callback = callback;
        }

        /// <summary>
        /// Loads the RPC settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public void Load(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.Mine = config.GetOrDefault<bool>("mine", false);
            if (this.Mine)
                this.MineAddress = config.GetOrDefault<string>("mineaddress", null);

            this.Stake = config.GetOrDefault<bool>("stake", false);
            if (this.Stake)
            {
                this.WalletName = config.GetOrDefault<string>("walletname", null);
                this.WalletPassword = config.GetOrDefault<string>("walletpassword", null);
            }

            this.callback?.Invoke(this);
        }

        /// <summary>
        /// Displays mining help information on the console.
        /// </summary>
        /// <param name="mainNet">Not used.</param>
        public static void PrintHelp(Network mainNet)
        {
            var defaults = NodeSettings.Default();
            var builder = new StringBuilder();

            builder.AppendLine("-mine=<0 or 1>            Enable POW mining.");
            builder.AppendLine("-stake=<0 or 1>           Enable POS.");
            builder.AppendLine("-mineaddress=<string>     The address to use for mining (empty string to select an address from the wallet).");
            builder.AppendLine("-walletname=<string>      The wallet name to use when staking.");
            builder.AppendLine("-walletpassword=<string>  Password to unlock the wallet.");

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
        }
    }
}
