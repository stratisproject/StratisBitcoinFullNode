using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.LightWallet
{
    public class LightWalletBitcoinExternalFeePolicy : IWalletFeePolicy
    {
        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        private readonly Money maxTxFee;

        private static readonly HttpClient HttpClient = new HttpClient();

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly ILogger logger;

        private readonly NodeSettings nodeSettings;

        private bool initializedOnce;

        private FeeRate highTxFeePerKb;

        private FeeRate mediumTxFeePerKb;

        private FeeRate lowTxFeePerKb;

        public LightWalletBitcoinExternalFeePolicy(IAsyncLoopFactory asyncLoopFactory, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, NodeSettings settings)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeSettings = settings;
            this.maxTxFee = new Money(0.1M, MoneyUnit.BTC);
            this.initializedOnce = false;
        }

        /// <inheritdoc />
        public void Start()
        {
            this.asyncLoop = this.asyncLoopFactory.Run(nameof(LightWalletBitcoinExternalFeePolicy), async token =>
            {
                // This will run evry 3 to 10 minutes randomly
                // So the API provider is not able to identify our transaction with a timing attack
                int waitMinutes = new Random().Next(3, 10);

                HttpResponseMessage response = null;
                try
                {
                    // TestNet fee estimation is useless, because the miners don't select transactions, like mainnet miners
                    // Test results on RegTest and TestNet are more illustrative with mainnet fees
                    response =
                        await HttpClient.GetAsync(@"http://api.blockcypher.com/v1/btc/main", HttpCompletionOption.ResponseContentRead, token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TimeoutException)
                {
                    response = null;
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    // If it's already been initialized once just keep using the feerate we already have
                    if (this.initializedOnce)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(waitMinutes), token).ContinueWith(t => { }).ConfigureAwait(false);
                        return;
                    }
                    else // Try again 3 seconds later, first time fee query is critical
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), token).ContinueWith(t => { }).ConfigureAwait(false);
                        return;
                    }
                }

                if (token.IsCancellationRequested) return;

                JObject json = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                this.lowTxFeePerKb = new FeeRate(new Money((int)(json.Value<decimal>("low_fee_per_kb")), MoneyUnit.Satoshi));
                this.mediumTxFeePerKb = new FeeRate(new Money((int)(json.Value<decimal>("medium_fee_per_kb")), MoneyUnit.Satoshi));
                this.highTxFeePerKb = new FeeRate(new Money((int)(json.Value<decimal>("high_fee_per_kb")), MoneyUnit.Satoshi));

                this.initializedOnce = true;

                if (token.IsCancellationRequested) return;

                await Task.Delay(TimeSpan.FromMinutes(waitMinutes), token).ContinueWith(t => { }).ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.Second,
            startAfter: TimeSpans.Second);
        }

        public void Stop()
        {
            this.asyncLoop.Dispose();
        }

        /// <inheritdoc />
        public Money GetRequiredFee(int txBytes)
        {
            return Math.Max(this.lowTxFeePerKb.GetFee(txBytes), this.nodeSettings.MinRelayTxFeeRate.GetFee(txBytes));
        }

        /// <inheritdoc />
        public Money GetMinimumFee(int txBytes, int confirmTarget)
        {
            // payTxFee is the user-set global for desired feerate
            return this.GetMinimumFee(txBytes, confirmTarget, this.lowTxFeePerKb.GetFee(txBytes));
        }

        /// <inheritdoc />
        public Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee)
        {
            Money feeNeeded = targetFee;

            feeNeeded = this.lowTxFeePerKb.GetFee(txBytes);
            if (confirmTarget < 50) feeNeeded = this.mediumTxFeePerKb.GetFee(txBytes);
            if (confirmTarget < 20) feeNeeded = this.highTxFeePerKb.GetFee(txBytes);

            // prevent user from paying a fee below minRelayTxFee or minTxFee
            feeNeeded = Math.Max(feeNeeded, this.GetRequiredFee(txBytes));
            // But always obey the maximum
            if (feeNeeded > this.maxTxFee)
                feeNeeded = this.maxTxFee;
            return feeNeeded;
        }

        /// <inheritdoc />
        public FeeRate GetFeeRate(int confirmTarget)
        {
            FeeRate feeRate = this.lowTxFeePerKb;
            if (confirmTarget < 50) feeRate = this.mediumTxFeePerKb;
            if (confirmTarget < 20) feeRate = this.highTxFeePerKb;
            return feeRate;
        }
    }
}
