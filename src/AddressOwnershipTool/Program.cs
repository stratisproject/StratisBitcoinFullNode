using System;
using System.IO;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository;

namespace AddressOwnershipTool
{
    class Program
    {
        static void Main(string[] args)
        {
            string arg = null;
            bool testnet;
            string destinationAddress = null;

            // Settings common between all modes
            testnet = args.Contains("-testnet");

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

            // Settings related to a stratisX wallet
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

                StratisXExport(privKeyFile, destinationAddress, testnet);

                Console.WriteLine("Finished");

                return;
            }

            // Settings related to an SBFN wallet, whether sqlite or JSON
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

            SbfnExport(walletName, walletPassword, destinationAddress, deepExport, testnet);

            Console.WriteLine("Finished");
        }

        static void StratisXExport(string privKeyFile, string destinationAddress, bool testnet = false)
        {
            Network network = testnet ? new StratisTest() : new StratisMain();

            var lines = File.ReadLines(privKeyFile);

            foreach (var line in lines)
            {
                // Skip comments
                if (line.Trim().StartsWith("#"))
                    continue;

                // If it isn't at least long enough to contain the WIF then ignore the line
                if (line.Trim().Length < 53)
                    continue;

                try
                {
                    string[] data = line.Trim().Split(" ");

                    string privKey = data[0];

                    Key privateKey = Key.Parse(privKey, network);

                    string address = privateKey.PubKey.GetAddress(network).ToString();

                    string message = $"{address}";

                    string signature = privateKey.SignMessage(message);

                    OutputToFile(address, destinationAddress, signature);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error creating signature with private key file line '{line}'");
                }
            }
        }

        static void SbfnExport(string walletName, string walletPassword, string destinationAddress, bool deepExport = false, bool testnet = false)
        {
            Network network = testnet ? new StratisTest() : new StratisMain();
            var nodeSettings = new NodeSettings(network);

            // First check if sqlite wallet is being used.
            var walletRepository = new SQLiteWalletRepository(nodeSettings.LoggerFactory, nodeSettings.DataFolder, nodeSettings.Network, new DateTimeProvider(), new ScriptAddressReader());
            walletRepository.Initialize(false);

            Wallet wallet = null;

            try
            {
                wallet = walletRepository.GetWallet(walletName);
            }
            catch
            {
            }

            if (wallet != null)
            {
                SqlExport(wallet, walletPassword, destinationAddress, deepExport);

                return;
            }

            Console.WriteLine($"No SQL wallet with name {walletName} was found in folder {nodeSettings.DataDir}! Checking for legacy JSON wallet.");

            var fileStorage = new FileStorage<Wallet>(nodeSettings.DataFolder.WalletPath);

            if (fileStorage.Exists(walletName + ".wallet.json"))
            {
                wallet = fileStorage.LoadByFileName(walletName + ".wallet.json");
            }

            if (wallet != null)
            {
                JsonExport(wallet, walletPassword, destinationAddress, deepExport);

                return;
            }

            Console.WriteLine($"No legacy wallet with name {walletName} was found in folder {nodeSettings.DataFolder.WalletPath}!");
        }

        static void SqlExport(Wallet wallet, string walletPassword, string destinationAddress, bool deepExport = false)
        {
            foreach (HdAddress address in wallet.GetAllAddresses())
            {
                if (address.Transactions.Count == 0 && !deepExport)
                    continue;

                ExportAddress(wallet, address, walletPassword, destinationAddress);
            }
        }

        static void JsonExport(Wallet wallet, string walletPassword, string destinationAddress, bool deepExport = false)
        {
            foreach (HdAddress address in wallet.GetAllAddresses())
            {
                if (address.Transactions.Count == 0 && !deepExport)
                    continue;

                ExportAddress(wallet, address, walletPassword, destinationAddress);
            }
        }

        static void ExportAddress(Wallet wallet, HdAddress address, string walletPassword, string destinationAddress)
        {
            ISecret privateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, address);

            string message = $"{address.Address}";

            string signature = privateKey.PrivateKey.SignMessage(message);

            OutputToFile(address.Address, destinationAddress, signature);
        }

        static void OutputToFile(string address, string destinationAddress, string signature)
        {
            string export = $"{address};{destinationAddress};{signature}";

            Console.WriteLine(export);

            using (StreamWriter sw = File.AppendText(destinationAddress + ".csv"))
            {
                sw.WriteLine(export);
            }
        }
    }
}
