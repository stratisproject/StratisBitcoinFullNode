using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Serialization;

namespace Stratis.SmartContracts.Tools.Sct
{
    public sealed class ValidatorService
    {
        public ValidationServiceResult Validate(string fileName, string source, IConsole console, string[] parameters)
        {
            var validationServiceResult = new ValidationServiceResult();

            CompileContract(source, console, validationServiceResult);
            ValidateContract(fileName, console, parameters, validationServiceResult);

            return validationServiceResult;
        }

        private static void BuildModuleDefinition(IConsole console, ValidationServiceResult validationServiceResult, out byte[] compilation, out IContractModuleDefinition moduleDefinition)
        {
            console.WriteLine("Building ModuleDefinition...");

            compilation = validationServiceResult.CompilationResult.Compilation;
            moduleDefinition = ContractDecompiler.GetModuleDefinition(compilation, new DotNetCoreAssemblyResolver()).Value;
            console.WriteLine("ModuleDefinition built successfully.");

            console.WriteLine();
        }

        private static void CompileContract(string source, IConsole console, ValidationServiceResult validationServiceResult)
        {
            console.WriteLine($"Compiling...");
            validationServiceResult.CompilationResult = ContractCompiler.Compile(source);
            if (!validationServiceResult.CompilationResult.Success)
                console.WriteLine("Compilation failed!");
            else
                console.WriteLine($"Compilation OK");

            console.WriteLine();
        }

        private static void ValidateContract(string fileName, IConsole console, string[] parameters, ValidationServiceResult validationServiceResult)
        {
            byte[] compilation;
            IContractModuleDefinition moduleDefinition;

            BuildModuleDefinition(console, validationServiceResult, out compilation, out moduleDefinition);

            console.WriteLine($"Validating file {fileName}...");

            Assembly smartContract = Assembly.Load(compilation);

            // Network does not matter here as we are only checking the deserialized Types of the params.
            var serializer = new MethodParameterStringSerializer(new SmartContractsRegTest());
            object[] methodParameters = null;
            if (parameters.Length != 0)
            {
                methodParameters = serializer.Deserialize(parameters);
            }

            validationServiceResult.ConstructorExists = Contract.ConstructorExists(smartContract.ExportedTypes.FirstOrDefault(), methodParameters);

            if (!validationServiceResult.ConstructorExists)
            {
                console.WriteLine("Smart contract construction failed.");
                console.WriteLine("No constructor exists with the provided parameters.");
            }

            validationServiceResult.DeterminismValidationResult = new SctDeterminismValidator().Validate(moduleDefinition);
            validationServiceResult.FormatValidationResult = new SmartContractFormatValidator().Validate(moduleDefinition.ModuleDefinition);
            if (!validationServiceResult.DeterminismValidationResult.IsValid || !validationServiceResult.FormatValidationResult.IsValid)
                console.WriteLine("Smart Contract failed validation. Run validate [FILE] for more info.");

            console.WriteLine();
        }
    }

    public sealed class ValidationServiceResult
    {
        public ContractCompilationResult CompilationResult { get; set; }
        public SmartContractValidationResult DeterminismValidationResult { get; set; }
        public SmartContractValidationResult FormatValidationResult { get; set; }
        public bool ConstructorExists { get; set; }

        public bool Success
        {
            get
            {
                return
                     this.CompilationResult.Success &&
                     this.FormatValidationResult.IsValid &&
                     this.DeterminismValidationResult.IsValid &&
                     this.ConstructorExists;
            }
        }
    }
}