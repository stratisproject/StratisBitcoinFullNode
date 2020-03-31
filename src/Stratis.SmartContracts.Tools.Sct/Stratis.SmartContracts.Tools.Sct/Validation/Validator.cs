using McMaster.Extensions.CommandLineUtils;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.Tools.Sct.Report;
using Stratis.SmartContracts.Tools.Sct.Report.Sections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.Tools.Sct.Validation
{
    [Command(Description = "Validates smart contracts for structure and determinism")]
    [HelpOption]
    class Validator
    {
        [Argument(0, Description = "The paths of the files to validate",
            Name = "[FILES]")]
        public List<string> InputFiles { get; }

        [Option("-sb|--showbytes", CommandOptionType.NoValue,
            Description = "Show contract compilation bytes")]
        public bool ShowBytes { get; }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            if (!this.InputFiles.Any())
            {
                app.ShowHelp();
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("Smart Contract Validator");
            Console.WriteLine();

            var determinismValidator = new SctDeterminismValidator();
            var formatValidator = new SmartContractFormatValidator();
            var warningValidator = new SmartContractWarningValidator();

            var reportData = new List<ValidationReportData>();

            foreach (string file in this.InputFiles)
            {
                var validationData = new ValidationReportData
                {
                    FileName = file,
                    CompilationErrors = new List<CompilationError>(),
                    DeterminismValidationErrors = new List<ValidationResult>(),
                    FormatValidationErrors = new List<ValidationError>(),
                    Warnings = new List<Warning>()
                };

                reportData.Add(validationData);

                ContractCompilationResult compilationResult = CompilationLoader.CompileFromFileOrDirectoryName(file, console);

                // Check if the file was found.
                if (compilationResult == null)
                {
                    return 1;
                }

                validationData.CompilationSuccess = compilationResult.Success;

                if (!compilationResult.Success)
                {
                    Console.WriteLine("Compilation failed!");
                    Console.WriteLine();

                    validationData.CompilationErrors
                        .AddRange(compilationResult
                            .Diagnostics
                            .Select(d => new CompilationError { Message = d.ToString() }));

                    continue;
                }

                validationData.CompilationBytes = compilationResult.Compilation;

                Console.WriteLine($"Compilation OK");
                Console.WriteLine();

                byte[] compilation = compilationResult.Compilation;

                Console.WriteLine("Building ModuleDefinition");

                IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(compilation, new DotNetCoreAssemblyResolver()).Value;

                Console.WriteLine("ModuleDefinition built successfully");
                Console.WriteLine();

                Console.WriteLine($"Validating file {file}...");
                Console.WriteLine();

                SmartContractValidationResult formatValidationResult = formatValidator.Validate(moduleDefinition.ModuleDefinition);

                validationData.FormatValid = formatValidationResult.IsValid;

                validationData
                    .FormatValidationErrors
                    .AddRange(formatValidationResult
                        .Errors
                        .Select(e => new ValidationError { Message = e.Message }));

                SmartContractValidationResult determinismValidationResult = determinismValidator.Validate(moduleDefinition);

                validationData.DeterminismValid = determinismValidationResult.IsValid;

                validationData
                    .DeterminismValidationErrors
                    .AddRange(determinismValidationResult.Errors);

                SmartContractValidationResult warningResult = warningValidator.Validate(moduleDefinition.ModuleDefinition);

                validationData
                    .Warnings
                    .AddRange(warningResult
                        .Errors
                        .Select(e => new Warning { Message = e.Message }));
            }

            List<IReportSection> reportStructure = new List<IReportSection>();
            reportStructure.Add(new HeaderSection());
            reportStructure.Add(new CompilationSection());

            reportStructure.Add(new FormatSection());
            reportStructure.Add(new DeterminismSection());

            reportStructure.Add(new WarningsSection());

            if (this.ShowBytes)
                reportStructure.Add(new ByteCodeSection());

            reportStructure.Add(new FooterSection());

            var renderer = new StreamTextRenderer(Console.Out);

            foreach (ValidationReportData data in reportData)
            {
                renderer.Render(reportStructure, data);
            }

            return 1;
        }
    }
}