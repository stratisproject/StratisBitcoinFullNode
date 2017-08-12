using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    /// <summary>
    /// A model representing the maximum amount a use can spend, along with the required fee.
    /// </summary>
    public class MaxSpendableAmountModel
    {
        /// <summary>
        /// Gets or sets the maximum spendable amount on an account.
        /// </summary>
        public Money MaxSpendableAmount { get; set; }

        /// <summary>
        /// Gets or sets the fee required the <see cref="MaxSpendableAmountModel.MaxSpendableAmount"/>.
        /// </summary>
        public Money Fee { get; set; }
    }
}
