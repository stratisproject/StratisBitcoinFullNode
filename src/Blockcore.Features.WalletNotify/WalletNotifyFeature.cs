using System;
using Stratis.Bitcoin.Builder.Feature;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Events;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Blockcore.Features.WalletNotify
{
    public class WalletNotifyFeature : FullNodeFeature
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly ISignals signals;

        /// <summary>The settings for the wallet feature.</summary>
        private readonly WalletSettings walletSettings;

        private SubscriptionToken transactionFoundSubscription;

        /// <summary>The shell command to execute.</summary>
        private string shellCommand;

        /// <summary>The shell arguments to send to the shell command.</summary>
        private string shellArguments;

        public WalletNotifyFeature(
            WalletSettings walletSettings,
            ISignals signals,
            ILoggerFactory loggerFactory)
        {
            Guard.NotNull(walletSettings, nameof(walletSettings));
            Guard.NotNull(signals, nameof(signals));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletSettings = walletSettings;
            this.signals = signals;
        }

        public override Task InitializeAsync()
        {
            // Only enable this feature if specified in the settings.
            if (string.IsNullOrWhiteSpace(this.walletSettings.WalletNotify))
            {
                return Task.CompletedTask;
            }

            this.logger.LogInformation($"-walletnotify was configured with command: {this.walletSettings.WalletNotify}.");

            var cmdArray = this.walletSettings.WalletNotify.Split(' ');

            this.shellCommand = cmdArray.First();
            this.shellArguments = string.Join(" ", cmdArray.Skip(1));

            this.transactionFoundSubscription = this.signals.Subscribe<TransactionFound>(ev => this.ProcessTransactionAndNotify(ev.FoundTransaction));

            
            this.logger.LogInformation($"-walletnotify was parsed as: {this.shellCommand} {this.shellArguments}");

            return Task.CompletedTask;
        }

        public void ProcessTransactionAndNotify(Transaction transaction)
        {
            try
            {
                var arguments = this.shellArguments.Replace("%s", transaction.ToString());
                this.logger.LogInformation($"-walletnotify running command: {this.shellCommand} {arguments}");
                RunCommand(this.shellCommand, arguments);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to parse and execute on -walletnotify.");
            }
        }

        public string RunCommand(string command, string args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Process.Start(startInfo);
            return string.Empty;
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderWalletNotifyExtension
    {
        public static IFullNodeBuilder UseWalletNotify(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<WalletNotifyFeature>("walletnotify");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<WalletNotifyFeature>()
                .FeatureServices(services =>
                {
                });
            });

            return fullNodeBuilder;
        }
    }
}
