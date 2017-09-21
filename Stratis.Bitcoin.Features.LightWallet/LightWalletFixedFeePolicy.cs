using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.LightWallet
{
    /// <summary>
    /// Light wallet fixed fee policy used for stratis network.
    /// </summary>
    public class LightWalletFixedFeePolicy : IWalletFeePolicy
    {
        /// <summary>Logger instance for this class.</summary>
        private readonly ILogger logger;

        /// <summary>Fixed fee rate to use for this policy.</summary>
        public FeeRate TxFeeRate { get; set; }

        /// <summary>
        /// Constructor for the light wallet fixed fee policy.
        /// </summary>
        /// <param name="loggerFactory">The factory for building logger instances.</param>
        public LightWalletFixedFeePolicy(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            // TODO: For now default is hardcoded, should probably be configurable 
            this.TxFeeRate = new FeeRate(10000);
        }

        /// <inheritdoc />
        public FeeRate GetFeeRate(int confirmTarget)
        {
            return this.TxFeeRate;
        }

        /// <inheritdoc />
        public Money GetMinimumFee(int txBytes, int confirmTarget)
        {
            return this.GetMinimumFee(txBytes, confirmTarget, Money.Zero);
        }

        /// <inheritdoc />
        public Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee)
        {
            return this.GetFeeRate(confirmTarget).GetFee(txBytes);
        }

        /// <inheritdoc />
        public Money GetRequiredFee(int txBytes)
        {
            return this.TxFeeRate.GetFee(txBytes);
        }

        /// <inheritdoc />
        public Task Initialize()
        {
            return Task.CompletedTask;
        }
    }
}
