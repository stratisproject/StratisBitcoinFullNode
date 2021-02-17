using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog.Targets;

namespace AddressOwnershipTool
{
    partial class Program
    {
        static async Task Main(string[] args)
        {
            string arg = null;
            bool validate;
            bool distribute;
            bool testnet;
            bool ledger;
            bool ignoreBalance;
            string destinationAddress = null;

            // Settings common between all modes
            testnet = args.Contains("-testnet");
            ledger = args.Contains("-ledger");
            ignoreBalance = args.Contains("-ignorebalance");

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

            var fileName = $"{(ledger ? "L-" : string.Empty)}{destinationAddress}.csv";

            if (!File.Exists(fileName))
            {
                using (StreamWriter sw = File.AppendText(fileName))
                {
                    sw.WriteLine("Address;Destination;Signature");
                }
            }

            if (ledger)
            {
                var numberOfAddressesToScan = 100;

                arg = args.FirstOrDefault(a => a.StartsWith("-addresscount"));
                if (arg != null)
                {
                    var numberOfAddressesToScanSetting = arg.Split('=')[1];
                    if (!int.TryParse(numberOfAddressesToScanSetting, out numberOfAddressesToScan))
                    {
                        Console.WriteLine($"Unable to parse '-addresscount' setting with a value of {numberOfAddressesToScanSetting}. Please use whole number.");

                        return;
                    }
                }

                arg = args.FirstOrDefault(a => a.StartsWith("-keypath"));
                string path = null;
                if (arg != null)
                {
                    path = arg.Split('=')[1];
                }

                var ledgerService = new LedgerService(testnet);

                try
                {
                    await ledgerService.ExportAddressesAsync(numberOfAddressesToScan, destinationAddress, ignoreBalance, path);
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Make sure your ledger device is unlocked and Stratis wallet is open.");
                }
                catch (LedgerWallet.LedgerWalletException)
                {
                    Console.WriteLine("Make sure your ledger device is unlocked and Stratis wallet is open.");
                }
                catch (ApplicationException ex)
                {
                    Console.WriteLine(ex.Message);
                }

                Console.WriteLine("Finished");

                return;
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
            {
                walletPassword = arg.Split('=')[1];
            }
            else
            {
                Console.WriteLine("Please enter wallet password to continue:");
                walletPassword = Console.ReadLine();
            }

            // Whether or not to export SBFN addresses with no transactions (may only be useful for a wallet that is not properly synced and therefore does not have full transaction history, but the user still needs to consider the gap limit).
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
