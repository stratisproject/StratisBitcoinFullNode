using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LedgerWallet;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Networks;

namespace AddressOwnershipTool
{
    public class LedgerService
    {
        private readonly Network network;

        private readonly BlockExplorerClient blockExplorerClient;

        private const int maximumInactiveAddresses = 20;

        public LedgerService(bool testnet)
        {
            this.network = testnet ? new StratisTest() : new StratisMain();
            this.blockExplorerClient = new BlockExplorerClient();
        }

        public async Task ExportAddressesAsync(int numberOfAddressesToScan, string destinationAddress, bool ignoreBalance, string keyPath = null)
        {
            LedgerClient ledger = (await LedgerClient.GetHIDLedgersAsync()).First();

            if (!string.IsNullOrEmpty(keyPath))
            {
                await ProcessAddressAsync(ledger, keyPath, ignoreBalance, destinationAddress);
                return;
            }

            bool foundInactiveAccount = false;

            for (int accountIndex = 0; !foundInactiveAccount; accountIndex++)
            {
                var addressChecks = new List<AddressCheckResult>();
                for (int addressIndex = 0; addressIndex < numberOfAddressesToScan; addressIndex++)
                {
                    var currentKeyPath = $"m/44'/105'/{accountIndex}'/0/{addressIndex}";

                    AddressCheckResult addressCheckResult = await this.ProcessAddressAsync(ledger, currentKeyPath, ignoreBalance, destinationAddress);
                    addressChecks.Add(addressCheckResult);

                    if (accountIndex == maximumInactiveAddresses - 1 && addressChecks.All(a => !a.HasActivity))
                    {
                        foundInactiveAccount = true;
                        break;
                    }
                }
            }
        }

        public void OutputToFile(string address, string destinationAddress, string signature)
        {
            string export = $"{address};{destinationAddress};{signature}";

            Console.WriteLine(export);

            using (StreamWriter sw = File.AppendText($"L-{destinationAddress}.csv"))
            {
                sw.WriteLine(export);
            }
        }

        private async Task<AddressCheckResult> ProcessAddressAsync(LedgerClient ledger, string keyPath, bool ignoreBalance, string destinationAddress)
        {
            var result = new AddressCheckResult(false, false);
            var key = new KeyPath(keyPath);

            GetWalletPubKeyResponse walletPubKey = await ledger.GetWalletPubKeyAsync(key);

            PubKey pubKey = walletPubKey.ExtendedPublicKey.PubKey;
            var address = walletPubKey.Address;

            var hasActivity = this.blockExplorerClient.HasActivity(address);
            result.HasActivity = hasActivity;

            if (!ignoreBalance)
            {
                Console.WriteLine($"Checking balance for {address}");
                if (!this.blockExplorerClient.HasBalance(address))
                    return result;

                result.HasBalance = true;
            }

            await ledger.PrepareMessage(key, address);
            var resp = await ledger.SignMessage();

            var signature = GetSignature(address, resp, pubKey);
            if (signature == null)
                return result;

            this.OutputToFile(address, destinationAddress, signature);

            return result;
        }

        private string GetSignature(string address, byte[] resp, PubKey pubKey)
        {
            // Convert the ASN.1 signature into the proper format for NBitcoin/NStratis to validate in the ownership tool
            // This is a very quick and nasty conversion that doesn't use much of the internal BC functionality
            // 31 - SET
            // <LEN>
            // 02 - INTEGER (R)
            // <LEN>
            // 02 - INTEGER (S)
            for (int i = 0; i < 2; i++)
            {
                int recId = i;

                if (resp[0] != (byte)(48 + recId))
                    continue; //throw new Exception("Unexpected signature encoding - outer type not SET");

                if (resp[2] != 0x02)
                    throw new Exception("Invalid signature encoding - type not integer");

                int rLength = resp[3];

                var rBytes = new byte[32];
                Array.Resize(ref rBytes, rLength); // can be 33
                Array.Copy(resp, 4, rBytes, 0, rLength);

                if (resp[4 + rLength] != 0x02)
                    throw new Exception("Invalid signature encoding - type not integer");

                int sLength = resp[5 + rLength];

                var sBytes = new byte[32];
                Array.Copy(resp, 6 + rLength, sBytes, 0, sLength);

                // Now we have to work backwards to figure out the recId needed to recover the signature.

                int headerByte = recId + 27 + (pubKey.IsCompressed ? 4 : 0);

                var sigData = new byte[65];  // 1 header + 32 bytes for R + 32 bytes for S

                sigData[0] = (byte)headerByte;

                Array.Copy(rBytes, rLength == 33 ? 1 : 0, sigData, 1, 32);
                Array.Copy(sBytes, sLength == 33 ? 1 : 0, sigData, 33, 32);

                var specialBytes = 0x18;
                var prefixBytes = Encoding.UTF8.GetBytes("Stratis Signed Message:\n");
                var lengthBytes = BitConverter.GetBytes((char)address.Length).Take(1).ToArray();
                var addressBytes = Encoding.UTF8.GetBytes(address);

                byte[] dataBytes = new byte[1 + prefixBytes.Length + lengthBytes.Length + addressBytes.Length];
                dataBytes[0] = (byte)specialBytes;
                Buffer.BlockCopy(prefixBytes, 0, dataBytes, 1, prefixBytes.Length);
                Buffer.BlockCopy(lengthBytes, 0, dataBytes, prefixBytes.Length + 1, lengthBytes.Length);
                Buffer.BlockCopy(addressBytes, 0, dataBytes, prefixBytes.Length + lengthBytes.Length + 1, addressBytes.Length);

                uint256 messageHash = NBitcoin.Crypto.Hashes.Hash256(dataBytes);
                var recovered = PubKey.RecoverCompact(messageHash, sigData);
                var recoveredAddress = recovered.Hash.ScriptPubKey.GetDestinationAddress(this.network).ToString();
                bool foundMatch = recoveredAddress == address;

                if (foundMatch)
                    return Encoders.Base64.EncodeData(sigData);
            }

            Console.WriteLine($"Failed to validate signature for address {address}");

            return null;
        }
    }
}
