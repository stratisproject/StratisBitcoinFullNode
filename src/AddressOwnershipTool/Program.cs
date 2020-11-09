using System;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Features.SQLiteWalletRepository;

namespace AddressOwnershipTool
{
    class Program
    {
        static void Main(string[] args)
        {
            string walletName = null;
            string walletPassword = null;
            
            string arg = args.FirstOrDefault(a => a.StartsWith("-name"));
            if (arg != null)
                walletName = arg.Split('=')[1];

            arg = args.FirstOrDefault(a => a.StartsWith("-password"));
            if (arg != null)
                walletPassword = arg.Split('=')[1];

            // Whether or not to export addresses with no transactions (may only be useful for a wallet that is not properly synced).
            bool deepExport = args.Contains("-deep");

            Network network = args.Contains("-testnet") ? new StratisTest() : new StratisMain();
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
                SqlExport(wallet, walletPassword, deepExport);

                return;
            }

            Console.WriteLine($"No SQL wallet with name {walletName} was found in folder {nodeSettings.DataDir}! Checking for legacy wallet.");

            var fileStorage = new FileStorage<Wallet>(nodeSettings.DataFolder.WalletPath);

            if (fileStorage.Exists(walletName + ".wallet.json"))
            {
                wallet = fileStorage.LoadByFileName(walletName + ".wallet.json");
            }

            if (wallet != null)
            {
                JsonExport(wallet, walletPassword, deepExport);

                return;
            }

            Console.WriteLine($"No legacy wallet with name {walletName} was found in folder {nodeSettings.DataFolder.WalletPath}!");
        }

        static void SqlExport(Wallet wallet, string walletPassword, bool deepExport = false)
        {
            Console.WriteLine("Address;Signature");

            foreach (HdAddress address in wallet.GetAllAddresses())
            {
                if (address.Transactions.IsEmpty() && !deepExport)
                    continue;

                ExportAddress(wallet, address, walletPassword);
            }
        }

        static void JsonExport(Wallet wallet, string walletPassword, bool deepExport = false)
        {
            Console.WriteLine("Address;Signature");

            foreach (HdAddress address in wallet.GetAllAddresses())
            {
                if (address.Transactions.IsEmpty() && !deepExport)
                    continue;

                ExportAddress(wallet, address, walletPassword);
            }
        }

        static void ExportAddress(Wallet wallet, HdAddress address, string walletPassword)
        {
            ISecret privateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, address);

            string message = $"{address.Address}";

            string signature = privateKey.PrivateKey.SignMessage(message);

            string export = $"{message};{signature}";

            Console.WriteLine(export);
        }
    }
}
