using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.LightWallet
{
    /// <summary>
    /// Light wallet fixed fee policy used for stratis network.
    /// </summary>
    /// <seealso cref="https://github.com/stratisproject/stratisX/blob/master/src/wallet.cpp#L1437">StratisX fee calculation.</seealso>
    public class LightWalletFixedFeePolicy : IWalletFeePolicy
    {
        /// <summary>Logger instance for this class.</summary>
        private readonly ILogger logger;

        /// <summary>Fixed fee rate to use for this policy.</summary>
        public FeeRate TxFeeRate { get; set; }

        /// <summary>Minimum fee rate to use for this policy.</summary>
        public FeeRate MinTxFee { get; set; }

        /// <summary>
        /// Constructor for the light wallet fixed fee policy.
        /// </summary>
        /// <param name="loggerFactory">The factory for building logger instances.</param>
        /// <param name="settings">The node settings.</param>
        public LightWalletFixedFeePolicy(ILoggerFactory loggerFactory, NodeSettings settings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.MinTxFee = settings.MinTxFee;
            this.TxFeeRate = this.MinTxFee;
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
            return this.MinTxFee.GetFee(txBytes);
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
