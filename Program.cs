using System;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace FedKeyPairGen
{
    /*
        Stratis Federation KeyPair Generator v1.0.0.0 - Generates cryptographic key pairs for Sidechain Federation Members.
        Copyright(c) 2018 Stratis Group Limited

        usage:  fedkeypairgen [-name=<name>] [-folder=<output_folder>] [-pass=<password>] [-h]
         -h        This help message.

        Example:  fedkeypairgen
    */

    // The Stratis Federation KeyPair Generator is a console app that can be sent to Federation Members
    // in order to generate their Private (and Public) keys without a need to run a Node at this stage.
    // See the "Use Case - Generate Federation Member Key Pairs" located in the Requirements folder in the
    // project repo.

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Start with the banner.
                FedKeyPairGenManager.OutputHeader();

                bool help = args.Contains("-h");

                // Help command output the usage and examples text.
                if (help)
                {
                    FedKeyPairGenManager.OutputUsage();
                                  }

                Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                var pubKey = mnemonic.DeriveExtKey().PrivateKey.PubKey;

                Console.WriteLine($"-- Mnemonic --");
                Console.WriteLine($"Please keep the following 12 words for yourself and note them down in a secure place:");
                Console.WriteLine($"{string.Join(" ", mnemonic.Words)}");
                Console.WriteLine();
                Console.WriteLine($"-- To share with the sidechain generator --");
                Console.WriteLine($"1. Your pubkey: {Encoders.Hex.EncodeData((pubKey).ToBytes(false))}");
                Console.WriteLine($"2. Your ip address: if you're willing to. This is required to help the nodes connect when bootstrapping the network.");
                Console.WriteLine();

                // Write success message including warnings to keep secret private keys safe.
                FedKeyPairGenManager.OutputSuccess();
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                FedKeyPairGenManager.OutputErrorLine($"An error occurred: {ex.Message}");
                Console.WriteLine();
                FedKeyPairGenManager.OutputUsage();
            }
        }
    }
}
