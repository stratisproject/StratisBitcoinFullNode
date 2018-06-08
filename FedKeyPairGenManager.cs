using System;
using System.Reflection;

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
            Console.WriteLine("usage: fedkeypairgen [-h]");
            Console.WriteLine(" -h        This help message.");
            Console.WriteLine();
            Console.WriteLine("Example:  fedkeypairgen");
            Console.WriteLine();
        }

        // Output completion message and secret warning.
        public static void OutputSuccess()
        {
            Console.WriteLine();
            Console.WriteLine("Done!");
        }

        // On error we output in red. 
        public static void OutputErrorLine(string message)
        {
            var colorSaved = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = colorSaved;
        }
    }
}
