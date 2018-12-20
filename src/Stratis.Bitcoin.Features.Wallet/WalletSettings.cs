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
        /// A value indicating whether to create a default wallet and unlock it on startup. Wallet password is saved in configuration.
        /// </summary>
        public bool DefaultWallet { get; set; }

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
        /// Runs the specified shell script when new transactions is discovered in the wallet, also triggers when they are confirmed. Single argument is provided, which contains the transaction ID. The transaction ID is replaced with %s in the command.
        /// </summary>
        public string WalletNotify { get; set; }

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
            this.DefaultWallet = config.GetOrDefault<bool>("defaultwallet", false, this.logger);
            this.DefaultWalletPassword = config.GetOrDefault<string>("defaultwalletpassword", "default", this.logger);
            this.UnusedAddressesBuffer = config.GetOrDefault<int>("walletaddressbuffer", 20, this.logger);
            this.WalletNotify = config.GetOrDefault<string>("walletnotify", null, this.logger);
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
            builder.AppendLine("-defaultwallet=<0 or 1>         Creates a default wallet. Default: 0.");
            builder.AppendLine("-defaultwalletpassword=<string> Overrides the default wallet password.");
            builder.AppendLine("-walletnotify=<string>          Execute this command when a transaction is first seen and when it is confirmed.");
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
            builder.AppendLine("#Creates a default wallet and unlocks the wallet on startup when set to 1. Default: 0.");
            builder.AppendLine("#defaultwallet=0");
            builder.AppendLine("#defaultwalletpassword=<string>");
            builder.AppendLine("#Execute this command when a transaction is first seen and when it is confirmed.");
            builder.AppendLine("#walletnotify=<string>");
        }
    }
}
