using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using McMaster.Extensions.CommandLineUtils;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Tools.Sct.Deployment;

namespace Stratis.SmartContracts.Tools.Sct.Build
{
    [Command(Description = "Builds a smart contract and outputs a dll. For testing purposes.")]
    [HelpOption]
    class Builder
    {
        [Argument(0, Description = "The file containing the source code of the smart contract to deploy",
            Name = "<File>")]
        [Required]
        [FileExists]
        public string InputFile { get; }

        [Argument(1, Description = "The destination path",
            Name = "<Path>")]
        [Required]
        public string OutputPath { get; }

        public int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("Smart Contract Deployer");

            if (!File.Exists(this.InputFile))
            {
                console.WriteLine($"{this.InputFile} does not exist");
                return 1;
            }

            if (File.Exists(this.OutputPath))
            {
                console.WriteLine($"Output file already exists!");
                return 1;
            }

            string source;

            console.WriteLine($"Reading {this.InputFile}");

            using (var sr = new StreamReader(File.OpenRead(this.InputFile)))
            {
                source = sr.ReadToEnd();
            }

            console.WriteLine($"Read {this.InputFile} OK");

            if (string.IsNullOrWhiteSpace(source))
            {
                console.WriteLine($"Empty file at {this.InputFile}");
                return 1;
            }

            if (!Deployer.ValidateFile(this.InputFile, source, console, out SmartContractCompilationResult compilationResult))
            {
                console.WriteLine("Smart Contract failed validation. Run validate [FILE] for more info.");
                return 1;
            }

            this.WriteDll(compilationResult.Compilation);

            return 1;
        }

        private void WriteDll(byte[] compilation)
        {
            using (FileStream sw = File.OpenWrite(this.OutputPath))
            {
                sw.Write(compilation, 0, compilation.Length);
            }
        }
    }
}
