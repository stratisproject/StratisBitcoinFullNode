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
            string address = GetDestinationFromOpReturn(transaction);
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

            string hash = GetHashFromOpReturn(transaction);
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
        private string GetHashFromOpReturn(Transaction transaction)
        {
            string hash = null;
            foreach (var txOut in transaction.Outputs)
            {
                var data = txOut.ScriptPubKey.ToBytes();
                if ((OpcodeType)data[0] == OpcodeType.OP_RETURN)
                    hash = ConvertValidOpReturnDataToHash(data);
            }
            return hash;
        }

        private int GetBlockHeightFromOpReturn(Transaction transaction)
        {
            foreach (var txOut in transaction.Outputs)
            {
                var data = txOut.ScriptPubKey.ToBytes();
                if ((OpcodeType)data[0] != OpcodeType.OP_RETURN) continue;
                var asString = Encoding.UTF8.GetString(RemoveOpReturnOperator(data));
                if (int.TryParse(asString, out int blockHeight)) return blockHeight;
            }
            return -1;
        }


        // Examines the outputs of the transaction to see if an OP_RETURN is present.
        // Validates the base58 result against the counter chain network checksum.
        private string GetDestinationFromOpReturn(Transaction transaction)
        {
            string destination = null;
            foreach (var txOut in transaction.Outputs)
            {
                var data = txOut.ScriptPubKey.ToBytes();
                if ((OpcodeType)data[0] == OpcodeType.OP_RETURN)
                    destination = ConvertValidOpReturnDataToAddress(data);
            }
            return destination;
        }

        // Converts the raw bytes from the output into a BitcoinAddress.
        // The address is parsed using the target network bytes and returns null if validation fails.
        private string ConvertValidOpReturnDataToAddress(byte[] data)
        {
            // Remove the RETURN operator and convert the remaining bytes to our candidate address.
            string destination = Encoding.UTF8.GetString(RemoveOpReturnOperator(data));

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

        private string ConvertValidOpReturnDataToHash(byte[] data)
        {
            byte[] hashBytes = RemoveOpReturnOperator(data).ToArray(); ;

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

        private static byte[] RemoveOpReturnOperator(byte[] rawBytes)
        {
            return rawBytes.Skip(2).ToArray();
        }
    }
}
