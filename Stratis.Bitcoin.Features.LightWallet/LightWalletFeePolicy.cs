using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.LightWallet
{

    public class LightWalletFeePolicy : IWalletFeePolicy
    {
        private readonly Money maxTxFee;
        private readonly HttpClient httpClient;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly INodeLifetime nodeLifetime;
        private readonly ILogger logger;

        private FeeRate highTxFeePerKb;
        private FeeRate mediumTxFeePerKb;
        private FeeRate lowTxFeePerKb;

        public LightWalletFeePolicy(IAsyncLoopFactory asyncLoopFactory, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory)
        {
            this.highTxFeePerKb = null;
            this.mediumTxFeePerKb = null;
            this.lowTxFeePerKb = null;
            this.httpClient = new HttpClient();
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.maxTxFee = new Money(0.1M, MoneyUnit.BTC);
        }

        public Task Initialize()
        {
            return this.asyncLoopFactory.Run(nameof(LightWalletFeePolicy), async token =>
            {
                HttpResponseMessage response =
                    await this.httpClient.GetAsync(@"http://api.blockcypher.com/v1/btc/main", HttpCompletionOption.ResponseContentRead, token)
                    .ConfigureAwait(false);

                var json = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                this.lowTxFeePerKb = new FeeRate(new Money((int)(json.Value<decimal>("low_fee_per_kb")), MoneyUnit.Satoshi));
                this.mediumTxFeePerKb = new FeeRate(new Money((int)(json.Value<decimal>("medium_fee_per_kb")), MoneyUnit.Satoshi));
                this.highTxFeePerKb = new FeeRate(new Money((int)(json.Value<decimal>("high_fee_per_kb")), MoneyUnit.Satoshi));

                if (token.IsCancellationRequested) return;

                var waitMinutes = new Random().Next(3, 10);
                await Task.Delay(TimeSpan.FromMinutes(waitMinutes), token).ContinueWith(t => { }).ConfigureAwait(false);
            },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpans.Second,
                startAfter: TimeSpans.Second);
        }

        public Money GetRequiredFee(int txBytes)
        {
            return Math.Max(this.lowTxFeePerKb.GetFee(txBytes), MempoolValidator.MinRelayTxFee.GetFee(txBytes));
        }

        public Money GetMinimumFee(int txBytes, int confirmTarget)
        {
            // payTxFee is the user-set global for desired feerate
            return this.GetMinimumFee(txBytes, confirmTarget, this.lowTxFeePerKb.GetFee(txBytes));
        }

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

        public FeeRate GetFeeRate(int confirmTarget)
        {
            FeeRate feeRate = this.lowTxFeePerKb;
            if (confirmTarget < 50) feeRate = this.mediumTxFeePerKb;
            if (confirmTarget < 20) feeRate = this.highTxFeePerKb;
            return feeRate;
        }
    }
}
