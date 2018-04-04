using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Stratis.FederatedPeg;

namespace FedKeyPairGen
{
    // The FedKeyPairGenManager handles console output and input.
    internal static class FedKeyPairGenManager
    {
        // Standard information header.
        public static void OutputHeader()
        {
            Console.WriteLine($"Stratis Federation KeyPair Generator v{Assembly.GetEntryAssembly().GetName().Version.ToString()} - Generates cryptographic key pairs for Sidechain Federation Members.");
            Console.WriteLine("Copyright (c) 2018 Stratis Group Limited");
            Console.WriteLine();
        }

        // A standard usage message with examples.  This is output on -h command and also in some cases if validation fails.
        public static void OutputUsage()
        {
            Console.WriteLine("usage: fedkeypairgen [-name=<name>] [-folder=<output_folder>] [-pass=<passPhrase>] [-h]");
            Console.WriteLine(" -name     Your full name as recognised by your Federation Administrator.");
            Console.WriteLine(" -folder   The output folder where files will be written (default is current folder).");
            Console.WriteLine(" -pass     The pass word or phrase used to encrypt your private key file.");
            Console.WriteLine(" -h        This help message.");
            Console.WriteLine();
            Console.WriteLine("Example:  fedkeypairgen -name=\"John Smith\" -folder=\"C:\\KeyPairs\" -pass=\"secret\"");
            Console.WriteLine("          fedkeypairgen -name=\"John Smith\" -pass=\"secret\"                       (uses current directory)");
            Console.WriteLine("          fedkeypairgen                                                         (interactive)");
            Console.WriteLine();
        }

        // Output completion message and secret warning.
        public static void OutputSuccess()
        {
            Console.WriteLine();
            Console.WriteLine("Four key files were successfully output. Two of the files are PRIVATE keys that you must keep secret. Do not distribute these private keys.");
        }

        // On error we output in red. 
        public static void OutputErrorLine(string message)
        {
            var colorSaved = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = colorSaved;
        }

        // Ask and confirm a password/phrase for interactive mode.
        public static bool AskForPassPhrase(out string passPhrase)
        {
            Console.WriteLine("Please enter a pass phrase.  We will use this pass phrase to encrypt your private key.");
            Console.WriteLine("Keep this pass phrase safe.");
            Console.WriteLine();

            passPhrase = Console.ReadLine();
            Console.WriteLine("Please reenter your pass phrase.");

            string passwordReenter = Console.ReadLine();
            if (passPhrase != passwordReenter)
            {
                OutputErrorLine("The pass phrases must match.");
                return false;
            }
            return true;
        }

        // Ask and confirm a name for interactive mode.
        public static bool AskForName(out string name)
        {
            Console.WriteLine("Please enter a valid name.  Your name is used to identify you to your Sidechain Administrator.");

            name = Console.ReadLine();
            if (FederationMemberPrivate.IsValidName(name) == false)
            {
                OutputErrorLine("Invalid name. Please enter at least 3 characters and do not include underscore characters.");
                return false;
            }

            Console.WriteLine($"Thank you, {name}.");
            Console.WriteLine();
            return true;
        }
    }
}
