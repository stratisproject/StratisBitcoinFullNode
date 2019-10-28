using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public static class AccountBasedPaymentDetailsExtensions
    {
        /// <summary>
        /// Returns the change payment for the given <see cref="PaymentDetails"/> items, or null if no change payment is found.
        /// If transactions to the same address appear out of order, then it's possible that change outputs are incorrectly identified.
        /// </summary>
        /// <param name="payments">The payment details to search.</param>
        /// <param name="address">The address of the account used in the accounts-based wallet system.</param>
        /// <returns></returns>
        public static PaymentDetails ChangePaymentOrDefault(this IList<PaymentDetails> payments, string address)
        {
            return payments.FirstOrDefault(p => p.DestinationAddress == address);
        }
    }
}