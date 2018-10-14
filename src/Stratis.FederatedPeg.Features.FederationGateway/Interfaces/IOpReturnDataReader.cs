using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// Represents different types of data the reader can discern.
    /// </summary>
    public enum OpReturnDataType
    {
        Unknown = 0,
        Address = 10,
        Hash = 20,
        BlockHeight = 30
    }

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
        /// Interprets the inbound OP_RETURN data and tells us what type it is.
        /// </summary>
        /// <param name="transaction">The transaction we are examining.</param>
        /// <param name="opReturnDataType">Returns information about how the data was interpreted.</param>
        /// <returns>The relevant string or null of the type is Unknown.</returns>
        string GetStringFromOpReturn(Transaction transaction, out OpReturnDataType opReturnDataType);
    }
}