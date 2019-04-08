using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.Wallet.Helpers
{
    /// <summary>
    ///  Used for comparing a list of TransactionItemModel to each other.
    /// </summary>
    /// <remarks>
    ///  This specific comparer has been made to filter out duplicate payment listings in the case of a transaction with multiple inputs.
    ///  This causes multiple items inside the wallet data that all have the same amount, transaction/spending id and payment details.
    ///  However not all fields on this model are equal to each other so we only compare the ones we need.
    ///  See: https://github.com/stratisproject/Breeze/issues/175
    /// </remarks>
    public class SentTransactionItemModelComparer : IEqualityComparer<TransactionItemModel>
    {
        /// <summary>
        ///  Determines whether the specified objects are equal.
        /// </summary>
        /// <param name="x">The first object of type TransactionItemModel to compare.</param>
        /// <param name="y">The second object of type TransactionItemModel to compare.</param>
        /// <returns>true if the specified objects are equal; otherwise, false.</returns>
        public bool Equals(TransactionItemModel x, TransactionItemModel y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;

            bool propertiesAreEqual = (x.Id == y.Id &&
                x.ConfirmedInBlock == y.ConfirmedInBlock &&
                x.Type == y.Type && x.Amount == y.Amount && x.Payments.Count == y.Payments.Count);

            if (!propertiesAreEqual)
            {
                return false;
            }

            foreach (PaymentDetailModel payment in x.Payments)
            {
                // Make sure all payments in x have their equivalent in y.
                // Because we check the counts are equal there is no need to check from both sides.
                if (!y.Payments.Any(w => w.Amount == payment.Amount && w.DestinationAddress == payment.DestinationAddress))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns a hash code for the specified object.
        /// </summary>
        /// <param name="obj">The System.Object for which a hash code is to be returned.</param>
        /// <returns>A hash code for the specified object.</returns>
        public int GetHashCode(TransactionItemModel obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            int hashValue = obj.Id.GetHashCode() ^
                obj.ConfirmedInBlock.GetHashCode() ^
                obj.Type.GetHashCode() ^
                obj.Amount.GetHashCode();

            foreach (PaymentDetailModel payment in obj.Payments)
            {
                hashValue = hashValue ^ payment.Amount.GetHashCode() ^ payment.DestinationAddress.GetHashCode();
            }

            return hashValue;
        }
    }
}
