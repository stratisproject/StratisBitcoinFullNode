using System;
using System.IO;

namespace Stratis.FederatedPeg.IntegrationTests.Tools
{
    class Program
    {
        public const string DEFAULT_OUTPUT_FILE_NAME = "FederatedNetworkTest.ps1";


        static void Main(string[] args)
        {
            Console.WriteLine("Utility to generate Federated Network ps1 test Script!");

            Console.WriteLine($"Type the name of the output file (enter to use \"{DEFAULT_OUTPUT_FILE_NAME}\")");
            string input = Console.ReadLine();

            try
            {
                string outputFileName = Path.GetFullPath(string.IsNullOrWhiteSpace(input) ? DEFAULT_OUTPUT_FILE_NAME : input);
                if (Path.GetFileName(outputFileName).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    Console.WriteLine($"Invalid File Name");
                    return;
                }

                Console.WriteLine("Generating script...");
                string script = new FederatedNetworkScripts.FederatedNetworkScriptGenerator().GenerateScript();
                Console.WriteLine("--- GENERATED SCRIPT ---");
                Console.WriteLine(script);
                Console.WriteLine("--- END OF GENERATED SCRIPT ---");
                Console.WriteLine();
                Console.WriteLine();


                File.WriteAllText(outputFileName, script);
                Console.WriteLine($"Script saved at {outputFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot generate the script: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Press Any Key to exit");
                Console.ReadKey();
            }
        }
    }
}
