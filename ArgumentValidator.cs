using System;
using System.Linq;
using Stratis.FederatedPeg;

namespace FedKeyPairGen
{
    // This class validates the arguments. It also performs some related input and output.
    class ArgumentValidator
    {
        // Todo: Consider factoring out the input and ouput into the FedKeyPairGenManager.
        /// <summary>
        /// Validates the arguments.  Switches to interactive mode if validation fails.
        /// </summary>
        /// <param name="args">Incoming command line arguments.</param>
        /// <param name="help">Switch to show the help message and exit.</param>
        /// <param name="name">The Federation Member name.</param>
        /// <param name="folder">The folder where the key files are output.</param>
        /// <param name="password">The passphrase used to encrypt the private keys.</param>
        /// <returns></returns>
        public static bool ProcessArgs(string[] args, out bool help, out string name, out string folder, out string password)
        {
            // Read the args into strings and switches.
            Func<string, string> lookup =
                option => args.Where(s => s.StartsWith(option)).Select(s => s.Substring(option.Length))
                    .FirstOrDefault();

            help = args.Contains("-h");
            name = lookup("-name=");
            folder = lookup("-folder=");
            password = lookup("-pass=");

            // Help command output the usage and examples text.
            if (help)
            {
                FedKeyPairGenManager.OutputUsage();
                return false;
            }

            // Validate the name or switch to interactive mode if needed.
            if (!FederationMemberPrivate.IsValidName(name))
            {
                bool nameSuccess = FedKeyPairGenManager.AskForName(out name);
                if (nameSuccess == false) return false;
            }
            else
            {
                Console.WriteLine($"Welcome, {name}.", name);
                Console.WriteLine();
            }

            // Folder.
            if (folder == null) folder = string.Empty;      //means use CurrentDirectory

            // Password.
            if (password == null)
            {
                bool passwordSuccess = FedKeyPairGenManager.AskForPassword(out password);
                if (passwordSuccess == false) return false;
            }

            //all good
            return true;
        }
    }
}
