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
        public void SetStruct<T>(string key, T value) where T : struct
        {
        }

        public void SetArray(string key, Array a)
        {
            throw new NotImplementedException();
        }

        public byte[] GetBytes(string key)
        {
            return new byte[] { };
        }

        public char GetChar(string key)
        {
            return '\0';
        }

        public Address GetAddress(string key)
        {
            return new Address();
        }

        public bool GetBool(string key)
        {
            return false;
        }

        public int GetInt32(string key)
        {
            return 0;
        }

        public uint GetUInt32(string key)
        {
            return 0;
        }

        public long GetInt64(string key)
        {
            return 0;
        }

        public ulong GetUInt64(string key)
        {
            return 0;
        }

        public string GetString(string key)
        {
            return null;
        }

        public T GetStruct<T>(string key) where T : struct
        {
            return default(T);
        }

        public T[] GetArray<T>(string key)
        {
            throw new NotImplementedException();
        }

        public void SetBytes(string key, byte[] value)
        {
        }

        public void SetChar(string key, char value)
        {
        }

        public void SetAddress(string key, Address value)
        {
        }

        public void SetBool(string key, bool value)
        {
        }

        public void SetInt32(string key, int value)
        {
        }

        public void SetUInt32(string key, uint value)
        {
        }

        public void SetInt64(string key, long value)
        {
        }

        public void SetUInt64(string key, ulong value)
        {
        }

        public void SetString(string key, string value)
        {
        }

    }
}