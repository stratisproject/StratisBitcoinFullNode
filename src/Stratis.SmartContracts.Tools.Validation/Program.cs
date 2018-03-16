using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Options;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Tools.Validation.Report;
using Stratis.SmartContracts.Tools.Validation.Report.Sections;

namespace Stratis.SmartContracts.Tools.Validation
{
    class Program
    {
        static void Main(string[] args)
        {
            var inputFiles = new List<string>();
            var showHelp = false;
            var showVersion = false;
            var showContractBytes = false;

            var options = new OptionSet
            {
                { "i|input=", n => inputFiles.Add(n) },
                { "sb|showbytes", b => showContractBytes = b != null },
                { "v|version", v =>  showVersion = v != null },
                { "h|help", h => showHelp = h != null }
            };

            List<string> extra;

            try
            {
                extra = options.Parse(args);

                if (args == null || !args.Any() || extra.Any() || showHelp)
                {
                    ShowHelp(options);
                    return;
                }

                if (showVersion)
                {
                    ShowVersion();
                    return;
                }
            }
            catch (OptionException e)
            {
                Console.WriteLine("Error parsing command line args");
                return;
            }

            Console.WriteLine("Smart Contract Validator");

            var determinismValidator = new SmartContractDeterminismValidator();
            var formatValidator = new SmartContractFormatValidator();

            var compiler = new SmartContractCompiler();
            var decompiler = new SmartContractDecompiler();

            var reportData = new List<ValidationReportData>();

            foreach (string file in inputFiles)
            {
                string source;

                Console.WriteLine($"Reading {file}");

                using (var sr = new StreamReader(File.OpenRead(file)))
                {
                    source = sr.ReadToEnd();
                }

                Console.WriteLine($"Read {file} OK");

                if (string.IsNullOrWhiteSpace(source))
                {
                    Console.WriteLine($"Empty file at {file}");
                    continue;
                }

                var validationData = new ValidationReportData
                {
                    FileName = file,
                    CompilationErrors = new List<CompilationError>(),
                    DeterminismValidationErrors = new List<ValidationError>(),
                    FormatValidationErrors = new List<ValidationError>()
                };

                reportData.Add(validationData);

                Console.WriteLine($"Compiling...");
                SmartContractCompilationResult compilationResult = compiler.Compile(source);

                validationData.CompilationSuccess = compilationResult.Success;

                if (!compilationResult.Success)
                {
                    Console.WriteLine("Compilation failed!");

                    validationData.CompilationErrors
                        .AddRange(compilationResult
                            .Diagnostics
                            .Select(d => new CompilationError {Message = d.ToString()}));
            
                    continue;
                }

                validationData.CompilationBytes = compilationResult.Compilation;

                Console.WriteLine($"Compilation OK");

                byte[] compilation = compilationResult.Compilation;

                Console.WriteLine("Building ModuleDefinition");

                SmartContractDecompilation decompilation = decompiler.GetModuleDefinition(compilation, new DotNetCoreAssemblyResolver());

                Console.WriteLine("ModuleDefinition built successfully");

                Console.WriteLine($"Validating file {file}...");

                SmartContractValidationResult formatValidationResult = formatValidator.Validate(decompilation);

                validationData.FormatValid = formatValidationResult.Valid;

                validationData
                    .FormatValidationErrors
                    .AddRange(formatValidationResult
                        .Errors
                        .Select(e => new ValidationError { Message = e.Message }));              
                
                SmartContractValidationResult determinismValidationResult = determinismValidator.Validate(decompilation);

                validationData.DeterminismValid = determinismValidationResult.Valid;

                validationData
                    .DeterminismValidationErrors
                    .AddRange(determinismValidationResult
                        .Errors
                        .Select(e => new ValidationError { Message = e.Message }));
            }

            List<IReportSection> reportStructure = new List<IReportSection>();
            reportStructure.Add(new HeaderSection());
            reportStructure.Add(new CompilationSection());

            if(showContractBytes)
                reportStructure.Add(new ByteCodeSection());

            reportStructure.Add(new FormatSection());
            reportStructure.Add(new DeterminismSection());
            reportStructure.Add(new FooterSection());

            var renderer = new StreamTextRenderer(Console.Out);

            foreach (ValidationReportData data in reportData)
            {
                renderer.Render(reportStructure, data);
            }
            
            Console.ReadKey();
        }

        private static void ShowVersion()
        {
            Console.WriteLine("v0.0.1");
        }

        private static void ShowHelp(OptionSet options)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Validates a C# Smart Contract for execution on the Stratis Platform.");
            builder.AppendLine("Usage:");
            builder.AppendLine(" dotnet run <Stratis.SmartContracts.Tools.Validation.dll> [-i <path>] [-sb] [-v] [-h]");
            builder.AppendLine();
            builder.AppendLine("Command line arguments:");
            builder.AppendLine("-i/input=<Path>           An input file. Can be specified multiple times.");
            builder.AppendLine("-sb/showbytes             Show contract compilation bytes.");
            builder.AppendLine("-v/version                Displays the current version.");
            builder.AppendLine("-h/help                   Show this help.");

            Console.Write(builder.ToString());
        }
    }
}
