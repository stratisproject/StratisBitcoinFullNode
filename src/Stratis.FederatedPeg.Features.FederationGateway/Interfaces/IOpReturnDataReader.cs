using System;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// OP_RETURN data can be a hash, an address or unknown.
    /// This class interprets the data.
    /// Addresses are contained in the source transactions on the monitor chain whereas
    /// hashes are contained in the destination transaction on the counter chain and
    /// are used to pair transactions together.
    /// </summary>
    public interface IOpReturnDataReader
    {
        /// <summary>
        /// Tries to find a single OP_RETURN output that can be interpreted as an address.
        /// </summary>
        /// <param name="transaction">The transaction we are examining.</param>
        /// <returns>The address as a string, or null if nothing is found, or if multiple addresses are found.</returns>
        string TryGetTargetAddress(Transaction transaction);

        /// <summary>
        /// Tries to find a single OP_RETURN output that can be interpreted as a transaction id.
        /// </summary>
        /// <param name="transaction">The transaction we are examining.</param>
        /// <returns>The transaction id as a string, or null if nothing is found, or if multiple ids are found.</returns>
        string TryGetTransactionId(Transaction transaction);
    }
}