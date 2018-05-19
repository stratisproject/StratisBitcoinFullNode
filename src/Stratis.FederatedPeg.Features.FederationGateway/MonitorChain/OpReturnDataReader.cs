using System;
using System.Linq;
using System.Text;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// Represents different types of data the reader can discern.
    /// </summary>
    internal enum OpReturnDataType
    {
        Address,
        Hash,
        Unknown
    }

    /// <summary>
    /// OP_RETURN data can be a hash, an address or unknown.
    /// This class interprets the data.
    /// </summary>
    internal static class OpReturnDataReader
    {
        public static string GetStringFromOpReturn(ILogger logger, Network network, Transaction transaction, out OpReturnDataType opReturnDataType)
        {
            string address = GetDestinationFromOpReturn(logger, network, transaction);
            if (address != null)
            {
                opReturnDataType = OpReturnDataType.Address;
                return address;
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
        // Validates the base58 result against the counter chain network checksum.
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
            string destination = Encoding.UTF8.GetString(data).Remove(0, 2);

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
            // Remove the RETURN operator.
            byte[] hashBytes = data.Skip(2).ToArray(); ;

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
    }
}
