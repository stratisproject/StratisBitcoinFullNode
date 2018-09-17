using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// Represents different types of data the reader can discern.
    /// </summary>
    internal enum OpReturnDataType
    {
        Address,
        Hash,
        BlockHeight,
        Unknown
    }

    /// <summary>
    /// OP_RETURN data can be a hash, an address or unknown.
    /// This class interprets the data.
    /// Addresses are contained in the source transactions on the monitor chain whereas
    /// hashes are contained in the destination transaction on the counter chain and
    /// are used to pair transactions together. 
    /// </summary>
    internal static class OpReturnDataReader
    {
        /// <summary>
        /// Interprets the inbound OP_RETURN data and tells us what type it is.
        /// </summary>
        /// <param name="logger">The logger to use for diagnostic purposes.</param>
        /// <param name="network">The network we are monitoring.</param>
        /// <param name="transaction">The transaction we are examining.</param>
        /// <param name="opReturnDataType">Returns information about how the data was interpreted.</param>
        /// <returns>The relevant string or null of the type is Unknown.</returns>
        public static string GetStringFromOpReturn(ILogger logger, Network network, Transaction transaction, out OpReturnDataType opReturnDataType)
        {
            string address = GetDestinationFromOpReturn(logger, network, transaction);
            if (address != null)
            {
                opReturnDataType = OpReturnDataType.Address;
                return address;
            }


            int blockHeight = GetBlockHeightFromOpReturn(transaction);
            if (blockHeight != -1)
            {
                opReturnDataType = OpReturnDataType.BlockHeight;
                return blockHeight.ToString();
            }

            string hash = GetHashFromOpReturn(logger, transaction);
            if (hash != null)
            {
                opReturnDataType = OpReturnDataType.Hash;
                return hash;
            }

            opReturnDataType = OpReturnDataType.Unknown;
            return null;
        }

        // Examines the outputs of the transaction to see if an OP_RETURN is present.
        // Validates the uint256 format.
        private static string GetHashFromOpReturn(ILogger logger, Transaction transaction)
        {
            string hash = null;
            foreach (var txOut in transaction.Outputs)
            {
                var data = txOut.ScriptPubKey.ToBytes();
                if ((OpcodeType)data[0] == OpcodeType.OP_RETURN)
                    hash = ConvertValidOpReturnDataToHash(logger, data);
            }
            return hash;
        }

        private static int GetBlockHeightFromOpReturn(Transaction transaction)
        {
            int blockHeight = -1;
            foreach (var txOut in transaction.Outputs)
            {
                var data = txOut.ScriptPubKey.ToBytes();
                if ((OpcodeType) data[0] != OpcodeType.OP_RETURN) continue;
                var asString = Encoding.UTF8.GetString(data.RemoveReturnOperator());
                int.TryParse(asString, out blockHeight);
            }
            return blockHeight;
        }


        // Examines the outputs of the transaction to see if an OP_RETURN is present.
        // Validates the base58 result against the counter chain network checksum.
        private static string GetDestinationFromOpReturn(ILogger logger, Network network, Transaction transaction)
        {
            string destination = null;
            foreach (var txOut in transaction.Outputs)
            {
                var data = txOut.ScriptPubKey.ToBytes();
                if ((OpcodeType)data[0] == OpcodeType.OP_RETURN)
                    destination = ConvertValidOpReturnDataToAddress(logger, network, data);
            }
            return destination;
        }

        // Converts the raw bytes from the output into a BitcoinAddress.
        // The address is parsed using the target network bytes and returns null if validation fails.
        private static string ConvertValidOpReturnDataToAddress(ILogger logger, Network network, byte[] data)
        {
            // Remove the RETURN operator and convert the remaining bytes to our candidate address.
            string destination = Encoding.UTF8.GetString(data.RemoveReturnOperator());

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

        private static string ConvertValidOpReturnDataToHash(ILogger logger, byte[] data)
        {
            byte[] hashBytes = data.RemoveReturnOperator().ToArray(); ;

            // Attempt to parse the hash. Validates the uint256 string.
            try
            {
                var hash256 = new uint256(hashBytes);
                logger.LogInformation($"ConvertValidOpReturnDataToHash received {hash256}.");
                return hash256.ToString();
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Candidate hash {hashBytes} could not be converted to a valid uint256. Reason {ex.Message}.");
                return null;
            }
        }

        private static byte[] RemoveReturnOperator(this byte[] rawBytes)
        {
            return rawBytes.Skip(2).ToArray();
        }
    }
}
