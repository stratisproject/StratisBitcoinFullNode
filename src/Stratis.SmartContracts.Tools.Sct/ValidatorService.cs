using System;
using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.Lifecycle;
using Stratis.SmartContracts.Core.Serialization;
using Stratis.Validators.Net;

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

        private static void BuildModuleDefinition(IConsole console, ValidationServiceResult validationServiceResult, out byte[] compilation, out SmartContractDecompilation decompilation)
        {
            console.WriteLine("Building ModuleDefinition...");

            compilation = validationServiceResult.CompilationResult.Compilation;
            decompilation = SmartContractDecompiler.GetModuleDefinition(compilation, new DotNetCoreAssemblyResolver());
            console.WriteLine("ModuleDefinition built successfully.");

            console.WriteLine();
        }

        private static void CompileContract(string source, IConsole console, ValidationServiceResult validationServiceResult)
        {
            console.WriteLine($"Compiling...");
            validationServiceResult.CompilationResult = SmartContractCompiler.Compile(source);
            if (!validationServiceResult.CompilationResult.Success)
                console.WriteLine("Compilation failed!");
            else
                console.WriteLine($"Compilation OK");

            console.WriteLine();
        }

        private static void ValidateContract(string fileName, IConsole console, string[] parameters, ValidationServiceResult validationServiceResult)
        {
            byte[] compilation;
            SmartContractDecompilation decompilation;

            BuildModuleDefinition(console, validationServiceResult, out compilation, out decompilation);

            console.WriteLine($"Validating file {fileName}...");

            Assembly smartContract = Assembly.Load(compilation);

            var serializer = new MethodParameterSerializer();
            object[] methodParameters = null;
            if (parameters.Length != 0)
            {
                var methodParametersRaw = new MethodParameterSerializer().ToRaw(parameters);
                methodParameters = serializer.ToObjects(methodParametersRaw);
            }

            validationServiceResult.LifeCycleResult = SmartContractConstructor.Construct(smartContract.ExportedTypes.FirstOrDefault(), new ValidatorSmartContractState(), methodParameters);
            if (!validationServiceResult.LifeCycleResult.Success)
            {
                console.WriteLine("Smart contract construction failed.");
                console.WriteLine("If the smart contract is constructed with parameters, please ensure they are provided.");
            }

            validationServiceResult.DeterminismValidationResult = new SmartContractDeterminismValidator().Validate(decompilation.ModuleDefinition);
            validationServiceResult.FormatValidationResult = new SmartContractFormatValidator().Validate(decompilation.ModuleDefinition);
            if (!validationServiceResult.DeterminismValidationResult.IsValid || !validationServiceResult.FormatValidationResult.IsValid)
                console.WriteLine("Smart Contract failed validation. Run validate [FILE] for more info.");

            console.WriteLine();
        }
    }

    public sealed class ValidationServiceResult
    {
        public SmartContractCompilationResult CompilationResult { get; set; }
        public ValidationResult DeterminismValidationResult { get; set; }
        public ValidationResult FormatValidationResult { get; set; }
        public LifecycleResult LifeCycleResult { get; set; }

        public bool Success
        {
            get
            {
                return
                     this.CompilationResult.Success &&
                     this.FormatValidationResult.IsValid &&
                     this.DeterminismValidationResult.IsValid &&
                     this.LifeCycleResult.Success;
            }
        }
    }

    public sealed class ValidatorSmartContractState : ISmartContractState
    {
        public IBlock Block => new Block();

        public IMessage Message => new Message(new Address("0"), new Address("0"), 0, (Gas)0);

        public IPersistentState PersistentState
        {
            get
            {
                return new ValidatorPersistentState();
            }
        }

        public IGasMeter GasMeter => null;

        public IInternalTransactionExecutor InternalTransactionExecutor => null;

        public IInternalHashHelper InternalHashHelper => null;

        public Func<ulong> GetBalance => null;
    }

    public sealed class ValidatorPersistentState : IPersistentState
    {
        public ISmartContractList<T> GetList<T>(string name)
        {
            return null;
        }

        public ISmartContractMapping<V> GetMapping<V>(string name)
        {
            return null;
        }

        public T GetObject<T>(string key)
        {
            return default(T);
        }

        public void SetObject<T>(string key, T obj)
        {
        }
    }
}