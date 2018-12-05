using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public class OpReturnDataReader : IOpReturnDataReader
    {
        private readonly ILogger logger;

        private readonly Network network;

        public OpReturnDataReader(ILoggerFactory loggerFactory, Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
        }

        ///<inheritdoc />
        public string TryGetTargetAddress(Transaction transaction)
        {
            var opReturnAddresses = SelectBytesContentFromOpReturn(transaction)
                .Select(this.TryConvertValidOpReturnDataToAddress)
                .Where(s => s != null)
                .Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

            this.logger.LogDebug("Address(es) found in OP_RETURN(s) of transaction {0}: [{1}]",
                transaction.GetHash(), string.Join(",", opReturnAddresses));

            return opReturnAddresses.Count != 1 ? null : opReturnAddresses[0];
        }

        /// <inheritdoc />
        public string TryGetTransactionId(Transaction transaction)
        {
            var transactionId = SelectBytesContentFromOpReturn(transaction)
                .Select(this.TryConvertValidOpReturnDataToHash)
                .Where(s => s != null)
                .Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

            this.logger.LogDebug("Transaction Id(s) found in OP_RETURN(s) of transaction {0}: [{1}]",
                transaction.GetHash(), string.Join(",", transactionId));

            return transactionId.Count != 1 ? null : transactionId[0];
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
                var bitcoinAddress = network.ToCounterChainNetwork().Parse<BitcoinAddress>(destination);
                logger.LogInformation($"ConvertValidOpReturnDataToAddress received {destination} and network.Parse received {bitcoinAddress}.");
                return destination;
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Address {destination} could not be converted to a valid address. Reason {ex.Message}.");
                return null;
            }
        }

        private string TryConvertValidOpReturnDataToHash(byte[] data)
        {
            // Attempt to parse the hash. Validates the uint256 string.
            try
            {
                var hash256 = new uint256(data);
                logger.LogInformation($"ConvertValidOpReturnDataToHash received {hash256}.");
                return hash256.ToString();
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Candidate hash {data} could not be converted to a valid uint256. Reason {ex.Message}.");
                return null;
            }
        }

        private static byte[] RemoveOpReturnOperator(byte[] rawBytes)
        {
            return rawBytes.Skip(2).ToArray();
        }
    }
}