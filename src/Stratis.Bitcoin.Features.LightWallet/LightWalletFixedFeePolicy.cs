using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.LightWallet
{
    /// <summary>
    /// Light wallet fixed fee policy used for stratis network.
    /// </summary>
    /// <seealso cref="https://github.com/stratisproject/stratisX/blob/master/src/wallet.cpp#L1437">StratisX fee calculation.</seealso>
    public class LightWalletFixedFeePolicy : IWalletFeePolicy
    {
        /// <inheritdoc />
        public void Start()
        {
            return;
        }

        /// <inheritdoc />
        public void Stop()
        {
            return;
        }

        /// <summary>Logger instance for this class.</summary>
        private readonly ILogger logger;

        /// <summary>Fixed fee rate to use for this policy.</summary>
        public FeeRate TxFeeRate { get; set; }

        /// <summary>Minimum fee rate to use for this policy.</summary>
        public FeeRate FallbackTxFeeRate { get; set; }

        /// <summary>
        /// Constructor for the light wallet fixed fee policy.
        /// </summary>
        /// <param name="loggerFactory">The factory for building logger instances.</param>
        /// <param name="settings">The node settings.</param>
        public LightWalletFixedFeePolicy(ILoggerFactory loggerFactory, NodeSettings settings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.FallbackTxFeeRate = settings.FallbackTxFeeRate;
            this.TxFeeRate = this.FallbackTxFeeRate;
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
            return this.FallbackTxFeeRate.GetFee(txBytes);
        }

        /// <inheritdoc />
        public Money GetRequiredFee(int txBytes)
        {
            return this.TxFeeRate.GetFee(txBytes);
        }
    }
}
