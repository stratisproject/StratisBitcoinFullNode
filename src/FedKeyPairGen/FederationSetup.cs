using System;
using System.Reflection;

namespace FederationSetup
{
    // The FedKeyPairGenManager handles console output and input.
    internal static class FederationSetup
    {
        /// <summary>
        /// Print the utility's header and menu.
        /// </summary>
        public static void OutputHeader()
        {
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine($"Stratis Federation Set up v{Assembly.GetEntryAssembly().GetName().Version}");
            Console.WriteLine("Copyright (c) 2018 Stratis Group Limited");
            Console.WriteLine(Environment.NewLine);
        }

        /// <summary>
        /// Shows the help message woth examples.
        /// This is output on -h command and also in some cases if validation fails.
        /// </summary>
        public static void OutputMenu()
        {
            Console.WriteLine("Menu:");
            Console.WriteLine("g       Create genesis blocks for Mainnet, Testnet and Regtest.");
            Console.WriteLine("p       Create private and public keys for federation members.");  // ask members to create public and private -p (for the specfic network)  - 1 pubpriv for signing transactions and 1 for pubpriv key for mining
            Console.WriteLine("m       Create multi signature addresses for the federation wallets.");
            Console.WriteLine("        args: [<network>] [-quorum=<quorum>] [-fedpubkeys=<pubkey1, pubkey2, ..>]");
            Console.WriteLine("              network:    testnet or regtest or mainnet (default).");
            Console.WriteLine("              quorum:     The minimum number of federated members needed to sign transactions.");
            Console.WriteLine("              fedpubkeys: Federation members' public keys. Must have an odd number of up to fifteen members."); // // fed admin will do -m and number (3 qurom + the public keys for the signing of transactions)
            Console.WriteLine("              Example:    federationsetup -m testnet -quorum=2 -fedpubkeys=PublicKey1, PublicKey2, PublicKey3, PublicKey4, PublicKey5");
            Console.WriteLine("menu    Show this menu.");
            Console.WriteLine("exit    Close the utility.");
            Console.WriteLine(Environment.NewLine);
        }

        /// <summary>
        ///  Output completion message and secret warning.
        /// </summary>
        public static void OutputSuccess()
        {
            Console.WriteLine("Done!");
            Console.WriteLine();
        }

        /// <summary>
        /// Shows an error message, in red.
        /// </summary>
        /// <param name="message">The message to show.</param>
        public static void OutputErrorLine(string message)
        {
            var colorSaved = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = colorSaved;
        }
    }
}
