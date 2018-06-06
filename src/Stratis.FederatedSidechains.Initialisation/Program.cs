using System;
using System.IO;
using System.Linq;

namespace Stratis.FederatedSidechains.Initialisation
{
    public class Program
    {
        public static readonly FileInfo DefaultConfigFile = 
            new FileInfo(Path.Combine(Environment.CurrentDirectory, "sidechainInitialisationConfig.json"));
        public FileInfo ConfigFile { get; private set; }
        static void Main(string[] args)
        {
            var program = new Program();
            program.Run(args);
        }

        public void Run(string[] args = null)
        {
            if (args == null || args.Length == 0) ConfigFile = DefaultConfigFile;
            else ConfigFile = new FileInfo(args.First());
        }
    }
}
