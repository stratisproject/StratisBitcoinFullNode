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

        private bool verbose = false;

        public LedgerService(bool testnet, bool verbose)
        {
            this.network = testnet ? new StratisTest() : new StratisMain();
            this.blockExplorerClient = new BlockExplorerClient();
            this.verbose = verbose;
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

                Console.WriteLine($"Checking addresses for m/44'/105'/{accountIndex}");

                for (int addressIndex = 0; addressIndex < numberOfAddressesToScan; addressIndex++)
                {
                    var currentKeyPath = $"m/44'/105'/{accountIndex}'/0/{addressIndex}";
                    
                    AddressCheckResult addressCheckResult = await this.ProcessAddressAsync(ledger, currentKeyPath, ignoreBalance, destinationAddress);
                    addressChecks.Add(addressCheckResult);

                    if (addressIndex == maximumInactiveAddresses - 1 && addressChecks.All(a => !a.HasActivity))
                    {
                        foundInactiveAccount = true;
                        break;
                    }
                }
                
                if (foundInactiveAccount)
                    continue;

                // Now scan all change addresses if account was active
                for (int addressIndex = 0; addressIndex < numberOfAddressesToScan; addressIndex++)
                {
                    var currentKeyPath = $"m/44'/105'/{accountIndex}'/1/{addressIndex}";

                    await this.ProcessAddressAsync(ledger, currentKeyPath, ignoreBalance, destinationAddress);
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

                Console.WriteLine($"Balance Found for {keyPath} - Please confirm transaction on your ledger device.");
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

                if (this.verbose)
                {
                    Console.WriteLine($@"Signature response from ledger: {BitConverter.ToString(resp)}");
                }

                if (resp[0] != (byte)(48 + recId))
                    continue; //throw new Exception("Unexpected signature encoding - outer type not SET");

                if (resp[2] != 0x02)
                    throw new Exception("Invalid signature encoding - type not integer");

                int rLength = resp[3];

                byte[] rBytes = new byte[rLength];
                Array.Copy(resp, 4, rBytes, 0, rLength);

                if (resp[4 + rLength] != 0x02)
                    throw new Exception("Invalid signature encoding - type not integer");

                int sLength = resp[5 + rLength];

                byte[] sBytes = new byte[sLength];
                Array.Copy(resp, 6 + rLength, sBytes, 0, sLength);

                // Now we have to work backwards to figure out the recId needed to recover the signature.

                int headerByte = recId + 27 + (pubKey.IsCompressed ? 4 : 0);

                byte[] sigData = new byte[1 + 32 + 32];  // 1 header + 32 bytes for R + 32 bytes for S

                sigData[0] = (byte)headerByte;
                
                switch (rLength)
                {
                    case 31:
                        // Add 1 to destination index as there will be a leading zero to make up the 32 bytes
                        Array.Copy(rBytes, 0, sigData, 1 + 1, rLength);
                        break;
                    case 32:
                        // The 'typical' case - no additional offsets
                        Array.Copy(rBytes, 0, sigData, 1 + 0, rLength);
                        break;
                    case 33:
                        // Use sourceIndex = 1 as we trim off the leading byte
                        Array.Copy(rBytes, 1, sigData, 1 + 0, rLength - 1);
                        break;
                    default:
                        throw new Exception("Unexpected rLength: " + rLength);
                }

                switch (sLength)
                {
                    case 31:
                        Array.Copy(sBytes, 0, sigData, 33 + 1, sLength);
                        break;
                    case 32:
                        Array.Copy(sBytes, 0, sigData, 33 + 0, sLength);
                        break;
                    case 33:
                        Array.Copy(sBytes, 1, sigData, 33 + 0, sLength - 1);
                        break;
                    default:
                        throw new Exception("Unexpected sLength: " + sLength);

                int specialBytes = 0x18;
                byte[] prefixBytes = Encoding.UTF8.GetBytes("Stratis Signed Message:\n");
                byte[] lengthBytes = BitConverter.GetBytes((char)address.Length).Take(1).ToArray();
                byte[] addressBytes = Encoding.UTF8.GetBytes(address);

                byte[] dataBytes = new byte[1 + prefixBytes.Length + lengthBytes.Length + addressBytes.Length];
                dataBytes[0] = (byte)specialBytes;
                Buffer.BlockCopy(prefixBytes, 0, dataBytes, 1, prefixBytes.Length);
                Buffer.BlockCopy(lengthBytes, 0, dataBytes, prefixBytes.Length + 1, lengthBytes.Length);
                Buffer.BlockCopy(addressBytes, 0, dataBytes, prefixBytes.Length + lengthBytes.Length + 1, addressBytes.Length);

                uint256 messageHash = NBitcoin.Crypto.Hashes.Hash256(dataBytes);
                PubKey recovered = PubKey.RecoverCompact(messageHash, sigData);
                string recoveredAddress = recovered.Hash.ScriptPubKey.GetDestinationAddress(this.network).ToString();
                bool foundMatch = recoveredAddress == address;

                if (foundMatch)
                    return Encoders.Base64.EncodeData(sigData);
            }

            Console.WriteLine($"Failed to validate signature for address {address}");

            return null;
        }
    }
}
