using NBitcoin;

namespace Stratis.Features.SQLiteWalletRepository.External
{
    public interface IWalletAddressReadOnlyLookup
    {
        /// <summary>
        /// Determines if the address has been added to this collection.
        /// </summary>
        /// <param name="scriptPubKey">The public key hash script of the address.</param>
        /// <param name="address">An address identifier.</param>
        /// <returns><c>True</c> if the address exists or has been added tentatively.</returns>
        bool Contains(Script scriptPubKey, out AddressIdentifier address);
    }
}
