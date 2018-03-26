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
        /// Enable the node to stake.
        /// </summary>
        public bool SaveTransactionHex { get; private set; }

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
        {        
            this.callback = callback;
        }

        /// <summary>
        /// Loads the wallet settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public void Load(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            TextFileConfiguration config = nodeSettings.ConfigReader;
            this.SaveTransactionHex = config.GetOrDefault<bool>("savetrxhex", false);
            this.callback?.Invoke(this);
        }

        /// <summary>
        /// Displays wallet configuration help information on the console.
        /// </summary>
        /// <param name="mainNet">Not used.</param>
        public static void PrintHelp(Network mainNet)
        {
            var defaults = NodeSettings.Default();
            var builder = new StringBuilder();

            builder.AppendLine("-savetrxhex=<0 or 1>            Save the hex of transactions in the wallet file.");
            defaults.Logger.LogInformation(builder.ToString());
        }
    }
}
