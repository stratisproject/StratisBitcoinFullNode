using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog.Targets;

namespace AddressOwnershipTool
{
    partial class Program
    {
        static void Main(string[] args)
        {
            string arg = null;
            bool validate;
            bool distribute;
            bool testnet;
            string destinationAddress = null;

            // Settings common between all modes
            testnet = args.Contains("-testnet");

            validate = args.Contains("-validate");
            if (validate)
            {
                arg = args.FirstOrDefault(a => a.StartsWith("-sigfolder"));

                string sigFolder = arg.Split('=')[1];

                if (!Directory.Exists(sigFolder))
                {
                    Console.WriteLine($"Could not locate directory '{sigFolder}'!");
                    
                    return;
                }

                var addressOwnershipService = new AddressOwnershipService(testnet);

                addressOwnershipService.Validate(sigFolder);

                return;
            }

            distribute = args.Contains("-distribute");
            if (distribute)
            {
                // We don't need wallet credentials to simulate the send.
                Console.WriteLine("Doing a trial run of the distribution to obtain the overall amount to be sent...");

                var addressOwnershipService = new AddressOwnershipService(testnet);
                addressOwnershipService.BuildAndSendDistributionTransactions("", "", "", false);

                Console.WriteLine("Proceed with sending funds (y/n)?");

                int result = Console.Read();
                if (result != 121 && result != 89)
                {
                    Console.WriteLine("Exiting...");

                    return;
                }

                arg = args.FirstOrDefault(a => a.StartsWith("-distributionwalletname"));
                string distributionWalletName = arg.Split('=')[1];

                arg = args.FirstOrDefault(a => a.StartsWith("-distributionwalletpassword"));
                string distributionWalletPassword = arg.Split('=')[1];

                string distributionAccountName;
                arg = args.FirstOrDefault(a => a.StartsWith("-distributionwalletaccount"));
                if (arg != null)
                    distributionAccountName = arg.Split('=')[1];
                else
                    distributionAccountName = "account 0";

                if (string.IsNullOrWhiteSpace(distributionWalletName) || string.IsNullOrWhiteSpace(distributionWalletPassword) || string.IsNullOrWhiteSpace(distributionAccountName))
                {
                    Console.WriteLine("Cannot proceed with sending funds without the distribution wallet credentials!");

                    return;
                }

                addressOwnershipService.BuildAndSendDistributionTransactions(distributionWalletName, distributionWalletPassword, distributionAccountName, true);

                return;
            }

            // If we aren't validating signatures then it is presumed that a wallet signature export is required.
            // First check if a signature file exists already, else create one.
            arg = args.FirstOrDefault(a => a.StartsWith("-destination"));
            if (arg != null)
                destinationAddress = arg.Split('=')[1];

            Console.WriteLine("Address;Destination;Signature");

            if (!File.Exists(destinationAddress + ".csv"))
            {
                using (StreamWriter sw = File.AppendText(destinationAddress + ".csv"))
                {
                    sw.WriteLine("Address;Destination;Signature");
                }
            }

            // Settings related to a stratisX wallet export.
            string privKeyFile = null;

            arg = args.FirstOrDefault(a => a.StartsWith("-privkeyfile"));
            if (arg != null)
            {
                privKeyFile = arg.Split('=')[1];

                if (!File.Exists(privKeyFile))
                {
                    Console.WriteLine($"Unable to locate private key file {privKeyFile} for stratisX address ownership!");

                    return;
                }

                Console.WriteLine("Private key file provided, assuming stratisX address ownership is required");

                var addressOwnershipService = new AddressOwnershipService(testnet, false);
                addressOwnershipService.StratisXExport(privKeyFile, destinationAddress);

                Console.WriteLine("Finished");

                return;
            }

            // Settings related to an SBFN wallet, whether sqlite or JSON, or accessed via the API directly.
            string walletName = null;
            string walletPassword = null;

            arg = args.FirstOrDefault(a => a.StartsWith("-name"));
            if (arg != null)
                walletName = arg.Split('=')[1];

            arg = args.FirstOrDefault(a => a.StartsWith("-password"));
            if (arg != null)
                walletPassword = arg.Split('=')[1];

            // Whether or not to export SBFN addresses with no transactions (may only be useful for a wallet that is not properly synced).
            bool deepExport = args.Contains("-deep");

            bool api = args.Contains("-api");

            if (string.IsNullOrEmpty(walletName))
            {
                Console.WriteLine("No wallet name specified!");

                return;
            }

            if (api)
            {
                throw new NotImplementedException();

                Console.WriteLine("Attempting to extract address signatures from node API. The node needs to be running.");

                var client = new NodeApiClient(testnet ? "http://localhost:38221/api/" : "http://localhost:37221/api/");

                // Get accounts
                List<string> accounts = client.GetAccounts(walletName);

                foreach (string account in accounts)
                {
                    Console.WriteLine($"Querying account '{account}' from wallet '{walletName}'...'");

                    // Get addresses
                    List<string> addresses = client.GetAddresses(walletName, account);

                    foreach (string address in addresses)
                    {
                        Console.WriteLine($"Attempting to sign message with address '{address}...'");

                        string signature = client.SignMessage(walletName, walletPassword, address);

                        var addressOwnershipService = new AddressOwnershipService(testnet, false);
                        addressOwnershipService.OutputToFile(address, destinationAddress, signature);
                    }
                }
            }
            else
            {
                var addressOwnershipService = new AddressOwnershipService(testnet, false);
                addressOwnershipService.SbfnExport(walletName, walletPassword, destinationAddress, deepExport);
            }

            Console.WriteLine("Finished");
        }
    }
}
