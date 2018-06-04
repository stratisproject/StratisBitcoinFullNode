using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Configuration related to the wallet.
    /// </summary>
    public class WalletSettings
    {
        /// <summary>
        /// A value indicating whether the transactions hex representations should be saved in the wallet file.
        /// </summary>
        public bool SaveTransactionHex { get; set; }

        /// <summary>
        /// A value indicating whether the wallet being run is the light wallet or the full wallet.
        /// </summary>
        public bool IsLightWallet { get; set; }

        /// <summary>
        /// A callback allow changing the default settings.
        /// </summary>
        private readonly Action<WalletSettings> callback;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public WalletSettings()
        {
        }

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="callback">Callback routine to be called once the wallet settings are loaded.</param>
        public WalletSettings(Action<WalletSettings> callback = null)
            :this()
        {
            this.callback = callback;
        }

        /// <summary>
        /// Initializes an instance of the object. Also loads the settings.
        /// </summary>
        /// <param name="nodeSettings">The node settings to load.</param>
        /// <param name="callback">Callback routine to be called once the wallet settings are loaded.</param>
        public WalletSettings(NodeSettings nodeSettings, Action<WalletSettings> callback = null)
            :this(callback)
        {
            this.Load(nodeSettings);
        }

        /// <summary>
        /// Loads the wallet settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public void Load(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            ILogger logger = nodeSettings.LoggerFactory.CreateLogger(typeof(WalletSettings).FullName);

            logger.LogTrace("()");

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.SaveTransactionHex = config.GetOrDefault<bool>("savetrxhex", false);
            logger.LogDebug("SaveTransactionHex set to {0}.", this.SaveTransactionHex);

            this.callback?.Invoke(this);

            logger.LogTrace("(-)");
        }

        /// <summary>
        /// Displays wallet configuration help information on the console.
        /// </summary>
        /// <param name="mainNet">Not used.</param>
        public static void PrintHelp(Network mainNet)
        {
            var defaults = NodeSettings.Default();
            var builder = new StringBuilder();

            builder.AppendLine("-savetrxhex=<0 or 1>            Save the hex of transactions in the wallet file. Default: false.");
            defaults.Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Wallet Settings####");
            builder.AppendLine("#Save the hex of transactions in the wallet file. Default: 0.");
            builder.AppendLine("#savetrxhex=0");
        }
    }
}
