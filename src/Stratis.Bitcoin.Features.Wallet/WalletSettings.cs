﻿using System;
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

        public WalletSettings() : this(NodeSettings.Default())
        {
        }

        public WalletSettings(NodeSettings nodeSettings)
        {
            TextFileConfiguration config = nodeSettings.ConfigReader;
            this.SaveTransactionHex = config.GetOrDefault<bool>("savetrxhex", false);
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
