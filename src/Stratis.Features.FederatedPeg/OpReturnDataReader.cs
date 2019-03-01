using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.FederatedPeg.NetworkHelpers;

namespace Stratis.Features.FederatedPeg
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
        /// <param name="address">The address as a string, or null if nothing is found, or if multiple addresses are found.</param>
        /// <returns><c>true</c> if address was extracted; <c>false</c> otherwise.</returns>
        bool TryGetTargetAddress(Transaction transaction, out string address);

        /// <summary>
        /// Tries to find a single OP_RETURN output that can be interpreted as a transaction id.
        /// </summary>
        /// <param name="transaction">The transaction we are examining.</param>
        /// <param name="txId">The transaction id as a string, or null if nothing is found, or if multiple ids are found.</param>
        /// <returns><c>true</c> if transaction id was extracted; <c>false</c> otherwise.</returns>
        bool TryGetTransactionId(Transaction transaction, out string txId);
    }

    public class OpReturnDataReader : IOpReturnDataReader
    {
        private readonly ILogger logger;

        private readonly Network network;

        public OpReturnDataReader(ILoggerFactory loggerFactory, Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
        }

        /// <inheritdoc />
        public bool TryGetTargetAddress(Transaction transaction, out string address)
        {
            List<string> opReturnAddresses = SelectBytesContentFromOpReturn(transaction)
                .Select(this.TryConvertValidOpReturnDataToAddress)
                .Where(s => s != null)
                .Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

            this.logger.LogDebug("Address(es) found in OP_RETURN(s) of transaction {0}: [{1}]",
                transaction.GetHash(), string.Join(",", opReturnAddresses));

            if (opReturnAddresses.Count != 1)
            {
                address = null;
                return false;
            }

            address = opReturnAddresses[0];
            return true;
        }

        /// <inheritdoc />
        public bool TryGetTransactionId(Transaction transaction, out string txId)
        {
            List<string> transactionId = SelectBytesContentFromOpReturn(transaction)
                .Select(this.TryConvertValidOpReturnDataToHash)
                .Where(s => s != null)
                .Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

            this.logger.LogDebug("Transaction Id(s) found in OP_RETURN(s) of transaction {0}: [{1}]",
                transaction.GetHash(), string.Join(",", transactionId));

            if (transactionId.Count != 1)
            {
                txId = null;
                return false;
            }

            txId = transactionId[0];
            return true;
        }

        private static IEnumerable<byte[]> SelectBytesContentFromOpReturn(Transaction transaction)
        {
            return transaction.Outputs
                .Select(o => o.ScriptPubKey)
                .Where(s => s.IsUnspendable)
                .Select(s => s.ToBytes())
                .Select(RemoveOpReturnOperator);
        }

        // Converts the raw bytes from the output into a BitcoinAddress.
        // The address is parsed using the target network bytes and returns null if validation fails.
        private string TryConvertValidOpReturnDataToAddress(byte[] data)
        {
            // Remove the RETURN operator and convert the remaining bytes to our candidate address.
            string destination = Encoding.UTF8.GetString(data);

            // Attempt to parse the string. Validates the base58 string.
            try
            {
                var bitcoinAddress = this.network.ToCounterChainNetwork().Parse<BitcoinAddress>(destination);
                this.logger.LogTrace($"ConvertValidOpReturnDataToAddress received {destination} and network.Parse received {bitcoinAddress}.");
                return destination;
            }
            catch (Exception ex)
            {
                this.logger.LogTrace($"Address {destination} could not be converted to a valid address. Reason {ex.Message}.");
                return null;
            }
        }

        private string TryConvertValidOpReturnDataToHash(byte[] data)
        {
            // Attempt to parse the hash. Validates the uint256 string.
            try
            {
                var hash256 = new uint256(data);
                this.logger.LogTrace($"ConvertValidOpReturnDataToHash received {hash256}.");
                return hash256.ToString();
            }
            catch (Exception ex)
            {
                this.logger.LogTrace($"Candidate hash {data} could not be converted to a valid uint256. Reason {ex.Message}.");
                return null;
            }
        }

        private static byte[] RemoveOpReturnOperator(byte[] rawBytes)
        {
            return rawBytes.Skip(2).ToArray();
        }
    }
}