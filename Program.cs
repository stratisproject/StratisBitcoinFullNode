using System;
using System.IO;
using Stratis.FederatedPeg;

namespace FedKeyPairGen
{
    /*
        Stratis Federation KeyPair Generator v1.0.0.0 - Generates cryptographic key pairs for Sidechain Federation Members.
        Copyright(c) 2018 Stratis Group Limited

        usage:  fedkeypairgen [-name=<name>] [-folder=<output_folder>] [-pass=<passPhrase>] [-h]
         -name     Your full name as recognised by your Federation Administrator.
         -folder   The output folder where files will be written (default is current folder).
         -pass     The pass word or phrase used to encrypt your private key file.
         -h        This help message.

        Example:  fedkeypairgen -name="John Smith" -folder="C:\KeyPairs" -pass="secret"
                  fedkeypairgen -name="John Smith" -pass="secret"                           (uses current directory)
                  fedkeypairgen                                                             (interactive)
    */

    // The Stratis Federation KeyPair Generator is a console app that can be sent to Federation Members
    // in order to generate their Private (and Public) keys without a need to run a Node at this stage.
    // See the "Use Case - Generate Federation Member Key Pairs" located in the Requirements folder in the
    // project repo.

    // Todo: The password is referred to as a PassPhrase although it isn't really a passphrase.
    // Todo: The password/phrase has no format constraints such as length special characters.
    // Todo: Native .Net AES256 Rijndael encryption is used.  Consider know full node alternative.
    // Todo: Consider leveraging the FullNode Wallet mnemonic instead of the way it is handled here.
    // Todo: We might consider outputting a config snippet for the full node also. 
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Start with the banner.
                FedKeyPairGenManager.OutputHeader();

                // Validate arguments.  After this step we must have valid input.
                bool @continue = ArgumentValidator.ProcessArgs(args, out bool _, out string name, out string folder, out string passPhrase);
                if (!@continue) return;

                // Create the private version of the Federation Member. 
                var federationMemberPrivate = FederationMemberPrivate.CreateNew(name, passPhrase);

                // File integrity check.  Do not continue if any clashing files exist in the destination folder.
                var memberFolderManager = new MemberFolderManager(folder);
                if (memberFolderManager.CountKeyFilesForMember(name) > 0)
                    throw new InvalidOperationException(
                        $"FedKeyPairGen cannot continue. The specified folder already contains keys for {name}.");

                // Output a public and a private key for each chain (mainchain and sidechain).
                memberFolderManager.OutputKeys(federationMemberPrivate);

                // Write success message including warnings to keep secret private keys safe.
                FedKeyPairGenManager.OutputSuccess();
            }
            catch (DirectoryNotFoundException)
            {
                FedKeyPairGenManager.OutputErrorLine("The folder must exist.");
                Console.WriteLine();
                FedKeyPairGenManager.OutputUsage();
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
