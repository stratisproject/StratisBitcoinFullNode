using System;
using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Message = Stratis.SmartContracts.Core.Message;

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

            var serializer = new MethodParameterStringSerializer();
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

    public sealed class ValidatorSmartContractState : ISmartContractState
    {
        public IBlock Block => new Block();

        public IMessage Message => new Message(new Address("0"), new Address("0"), 0);

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

        public IContractLogger ContractLogger => null;

        public ISerializer Serializer => null;
    }

    public sealed class ValidatorPersistentState : IPersistentState
    {
        public byte[] GetBytes(string key)
        {
            throw new NotImplementedException();
        }

        public void SetBytes(string key, byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public char GetAsChar(string key)
        {
            throw new NotImplementedException();
        }

        public Address GetAsAddress(string key)
        {
            throw new NotImplementedException();
        }

        public bool GetAsBool(string key)
        {
            throw new NotImplementedException();
        }

        public int GetAsInt32(string key)
        {
            throw new NotImplementedException();
        }

        public uint GetAsUInt32(string key)
        {
            throw new NotImplementedException();
        }

        public long GetAsInt64(string key)
        {
            throw new NotImplementedException();
        }

        public ulong GetAsUInt64(string key)
        {
            throw new NotImplementedException();
        }

        public string GetAsString(string key)
        {
            throw new NotImplementedException();
        }

        public T GetAsStruct<T>(string key) where T : struct
        {
            throw new NotImplementedException();
        }

        public void SetChar(string key, char value)
        {
            throw new NotImplementedException();
        }

        public void SetAddress(string key, Address value)
        {
            throw new NotImplementedException();
        }

        public void SetBool(string key, bool value)
        {
            throw new NotImplementedException();
        }

        public void SetInt32(string key, int value)
        {
            throw new NotImplementedException();
        }

        public void SetUInt32(string key, uint value)
        {
            throw new NotImplementedException();
        }

        public void SetInt64(string key, long value)
        {
            throw new NotImplementedException();
        }

        public void SetUInt64(string key, ulong value)
        {
            throw new NotImplementedException();
        }

        public void SetString(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void SetStruct<T>(string key, T value) where T : struct
        {
            throw new NotImplementedException();
        }
    }
}