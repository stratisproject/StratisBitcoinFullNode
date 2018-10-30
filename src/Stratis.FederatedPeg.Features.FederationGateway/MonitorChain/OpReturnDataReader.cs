using System;
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

        /// <inheritdoc />
        public string GetStringFromOpReturn(Transaction transaction, out OpReturnDataType opReturnDataType)
        {
            if (!TryGetSingleOpReturnOutputContent(transaction, out var content))
            {
                opReturnDataType = OpReturnDataType.Unknown;
                return null;
            }

            string address = TryConvertValidOpReturnDataToAddress(content);
            if (address != null)
            {
                opReturnDataType = OpReturnDataType.Address;
                return address;
            }

            int blockHeight = TryConvertValidOpReturnDataToBlockHeight(content);
            if (blockHeight != -1)
            {
                opReturnDataType = OpReturnDataType.BlockHeight;
                return blockHeight.ToString();
            }

            string hash = TryConvertValidOpReturnDataToHash(content);
            if (hash != null)
            {
                opReturnDataType = OpReturnDataType.Hash;
                return hash;
            }

            opReturnDataType = OpReturnDataType.Unknown;
            return null;
        }

        private bool TryGetSingleOpReturnOutputContent(Transaction transaction, out byte[] content)
        {
            try
            {
                content = transaction.Outputs.Select(o => o.ScriptPubKey)
                    .SingleOrDefault(s => s.IsUnspendable).ToBytes();
                if (content == null) return false;

                content = RemoveOpReturnOperator(content);
                return true;
            }
            catch (Exception e)
            {
                this.logger.LogDebug("Failed to find a single OP_RETURN output in transaction {0}: {1}",
                    transaction.GetHash(), e.Message);
                content = null;
                return false;
            }
        }

        private int TryConvertValidOpReturnDataToBlockHeight(byte[] data)
        {

            var asString = Encoding.UTF8.GetString(data);
            if (int.TryParse(asString, out int blockHeight)) return blockHeight;

            return -1;
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

        ///<inheritdoc />
        public string TryGetTargetAddressFromOpReturn(Transaction transaction)
        {
            var opReturnAddresses = transaction.Outputs
                .Select(o => o.ScriptPubKey)
                .Where(s => s.IsUnspendable)
                .Select(s => s.ToBytes())
                .Select(RemoveOpReturnOperator)
                .Select(this.TryConvertValidOpReturnDataToAddress)
                .Where(s => s != null)
                .Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

            this.logger.LogDebug("Address(es) found in OP_RETURN(s) of transaction {0}: [{1}]",
                transaction.GetHash(), string.Join(",", opReturnAddresses));

            return opReturnAddresses.Count != 1 ? null : opReturnAddresses[0];
        }
    }
}