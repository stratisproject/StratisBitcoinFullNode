using System;
using System.Reflection;
using System.Text;

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
            var builder = new StringBuilder();

            builder.AppendLine($"Stratis Federation Set up v{Assembly.GetEntryAssembly().GetName().Version}");
            builder.AppendLine("Copyright (c) 2018 Stratis Group Limited");

            Console.WriteLine(builder);
        }

        /// <summary>
        /// Shows the help message woth examples.
        /// This is output on -h command and also in some cases if validation fails.
        /// </summary>
        public static void OutputMenu()
        {
            var builder = new StringBuilder();

            builder.AppendLine("Menu:");
            builder.AppendLine("g       Create genesis blocks for Mainnet, Testnet and Regtest.");
            builder.AppendLine("        args: [-text=\"<text>\"]");
            builder.AppendLine("              text:       A bit of text or a url to be included in the genesis block.");
            builder.AppendLine("              Example:    g -text=\"https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/\"");
            builder.AppendLine("p       Create private and public keys for federation members.");  // ask members to create public and private -p (for the specfic network)  - 1 pubpriv for signing transactions and 1 for pubpriv key for mining
            builder.AppendLine("        args: [-passphrase=<passphrase>] [-datadir=<datadir>] [-ismultisig=<bool>] (optional - space character not allowed)");
            builder.AppendLine("              passphrase: a passphrase used to derive the private key from the transaction signing mnenmonic");
            builder.AppendLine("              datadir:    optional arg, directory where private key is saved");
            builder.AppendLine("              ismultisig: optional arg, controls output");
            builder.AppendLine("              Example:    p -passphrase=h@rd2Gu3ss!");
            builder.AppendLine("              Example:    p -passphrase=h@rd2Gu3ss! -datadir=c:\\dev -ismultisig=true");
            builder.AppendLine("m       Create multi signature addresses for the federation wallets.");
            builder.AppendLine("        args: [-network=<network>] [-quorum=<quorum>] [-fedpubkeys=<pubkey1, pubkey2, ..>]");
            builder.AppendLine("              network:    mainnet, testnet or regtest.");
            builder.AppendLine("              quorum:     The minimum number of federated members needed to sign transactions.");
            builder.AppendLine("              fedpubkeys: Federation members' public keys. Up to fifteen members."); // // fed admin will do -m and number (3 qurom + the public keys for the signing of transactions)
            builder.AppendLine("              Example:    m -network=testnet -quorum=2 -fedpubkeys=PublicKey1,PublicKey2,PublicKey3,PublicKey4,PublicKey5");
            builder.AppendLine("r       Creates a transaction to move the multisig funds to a new federation. The transaction is stored in a (.hex) file in the data directory together with similar files obtained from other nodes.");
            builder.AppendLine("        args: [-network=<network>] [-datadir=<datadir>] [-quorum=<quorum>] [-fedpubkeys=<pubkey1, pubkey2, ..>] [-password=<password>] [-txtime=<txtime>]");
            builder.AppendLine("              network:    mainnet, testnet or regtest.");
            builder.AppendLine("              datadir:    optional arg, old federation directory where private key is saved.");
            builder.AppendLine("              quorum:     The minimum number of new federation members needed to sign transactions.");
            builder.AppendLine("              fedpubkeys: New federation members' public keys. Up to fifteen members."); // // fed admin will do -m and number (3 qurom + the public keys for the signing of transactions)
            builder.AppendLine("              password:   The old federation wallet password.");
            builder.AppendLine("              txtime:     The timestamp to add to the generated transaction. Corresponds to the agreed time when the old federation will be manually stopped.");
            builder.AppendLine("x       Creates a transaction to move the multisig funds to a new STRAX/CIRRUS federation (new redeem script format). The transaction is stored in a (.hex) file in the data directory together with similar files obtained from other nodes.");
            builder.AppendLine("        args: [-network=<network>] [-datadir=<datadir>] [-fedpubkeys=<pubkey1, pubkey2, ..>] [-password=<password>] [-txtime=<txtime>]");
            builder.AppendLine("              network:    mainnet, testnet or regtest.");
            builder.AppendLine("              datadir:    optional arg, old federation directory where private key is saved.");
            builder.AppendLine("              fedpubkeys: New federation members' public keys. Up to fifteen members.");
            builder.AppendLine("              password:   The old federation wallet password.");
            builder.AppendLine("              txtime:     The timestamp to add to the generated transaction. Corresponds to the agreed time when the old federation will be manually stopped.");
            builder.AppendLine("menu    Show this menu.");
            builder.AppendLine("exit    Close the utility.");

            Console.WriteLine(builder);
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
            ConsoleColor colorSaved = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = colorSaved;
        }
    }
}
