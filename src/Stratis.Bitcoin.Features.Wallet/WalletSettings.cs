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
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// A value indicating whether the transactions hex representations should be saved in the wallet file.
        /// </summary>
        public bool SaveTransactionHex { get; set; }

        /// <summary>
        /// A value indicating whether to unlock the supplied default wallet on startup.
        /// </summary>
        public bool UnlockDefaultWallet { get; set; }

        /// <summary>
        /// Name for the default wallet.
        /// </summary>
        public string DefaultWalletName { get; set; }

        /// <summary>
        /// Password for the default wallet if overriding the default.
        /// </summary>
        public string DefaultWalletPassword { get; set; }

        /// <summary>
        /// A value indicating whether the wallet being run is the light wallet or the full wallet.
        /// </summary>
        public bool IsLightWallet { get; set; }

        /// <summary>Size of the buffer of unused addresses maintained in an account.</summary>
        public int UnusedAddressesBuffer { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public WalletSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(WalletSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.SaveTransactionHex = config.GetOrDefault<bool>("savetrxhex", false, this.logger);
            this.UnusedAddressesBuffer = config.GetOrDefault<int>("walletaddressbuffer", 20, this.logger);
            this.DefaultWalletName = config.GetOrDefault<string>("defaultwalletname", null, this.logger);

            if (!string.IsNullOrEmpty(this.DefaultWalletName))
            {
                this.DefaultWalletPassword = config.GetOrDefault<string>("defaultwalletpassword", "default", null); // No logging!
                this.UnlockDefaultWallet = config.GetOrDefault<bool>("unlockdefaultwallet", false, this.logger);
            }
        }

        /// <summary>
        /// Check if the default wallet is specified.
        /// </summary>
        /// <returns>Returns true if the <see cref="DefaultWalletName"/> is other than empty string.</returns>
        public bool IsDefaultWalletEnabled()
        {
            return !string.IsNullOrWhiteSpace(this.DefaultWalletName);
        }

        /// <summary>
        /// Displays wallet configuration help information on the console.
        /// </summary>
        /// <param name="network">Not used.</param>
        public static void PrintHelp(Network network)
        {
            NodeSettings defaults = NodeSettings.Default(network);
            var builder = new StringBuilder();

            builder.AppendLine("-savetrxhex=<0 or 1>            Save the hex of transactions in the wallet file. Default: 0.");
            builder.AppendLine("-defaultwalletname=<string>     Loads the specified wallet on startup. If it doesn't exist, it will be created automatically.");
            builder.AppendLine("-defaultwalletpassword=<string> Overrides the default wallet password. Default: default.");
            builder.AppendLine("-unlockdefaultwallet=<0 or 1>   Unlocks the specified default wallet. Default: 0.");
            builder.AppendLine("-walletaddressbuffer=<number>   Size of the buffer of unused addresses maintained in an account. Default: 20.");
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
            builder.AppendLine("#Creates a wallet with the specified name and the specified password. It will be created if it doesn't exist and can be unlocked on startup when unlockdefaultwallet is set to 1.");
            builder.AppendLine("#defaultwalletname=");
            builder.AppendLine("#Overrides the default wallet password. Default: default.");
            builder.AppendLine("#defaultwalletpassword=default");
            builder.AppendLine("#A value indicating whether to unlock the supplied default wallet on startup. Default 0.");
            builder.AppendLine("#unlockdefaultwallet=0");
            builder.AppendLine("#Size of the buffer of unused addresses maintained in an account. Default: 20.");
            builder.AppendLine("#walletaddressbuffer=20");
        }
    }
}
