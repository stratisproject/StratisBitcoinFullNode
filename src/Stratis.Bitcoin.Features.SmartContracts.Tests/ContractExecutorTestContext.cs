using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Patricia;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Loader;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    /// <summary>
    /// A mock-less container for all the parts of contract execution.
    /// Most likely just used to get the VM but saves test rewriting for every change inside execution.
    /// </summary>
    public class ContractExecutorTestContext
    {
        public Network Network { get; }
        public IKeyEncodingStrategy KeyEncodingStrategy { get; }
        public ILoggerFactory LoggerFactory { get; }
        public ContractStateRoot State { get; }
        public SmartContractValidator Validator { get; }
        public IAddressGenerator AddressGenerator {get;}
        public ContractAssemblyLoader AssemblyLoader { get; }
        public IContractModuleDefinitionReader ModuleDefinitionReader { get; }
        public IContractPrimitiveSerializer ContractPrimitiveSerializer { get; }
        public IInternalTransactionExecutorFactory InternalTxExecutorFactory { get; }
        public ReflectionVirtualMachine Vm { get; }
        public ISmartContractStateFactory SmartContractStateFactory { get; }
        public StateProcessor StateProcessor { get; }

        public ContractExecutorTestContext()
        {
            this.Network = new SmartContractsRegTest();
            this.KeyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.LoggerFactory = new ExtendedLoggerFactory();
            this.LoggerFactory.AddConsoleWithFilters();
            this.State = new ContractStateRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.ContractPrimitiveSerializer = new ContractPrimitiveSerializer(this.Network);
            this.AddressGenerator = new AddressGenerator();
            this.Validator = new SmartContractValidator();
            this.AssemblyLoader = new ContractAssemblyLoader();
            this.ModuleDefinitionReader = new ContractModuleDefinitionReader();
            this.Vm = new ReflectionVirtualMachine(this.Validator, this.LoggerFactory, this.Network, this.AssemblyLoader, this.ModuleDefinitionReader);
            this.StateProcessor = new StateProcessor(this.Vm, this.AddressGenerator);
            this.InternalTxExecutorFactory = new InternalTransactionExecutorFactory(this.LoggerFactory, this.Network, this.StateProcessor);
            this.SmartContractStateFactory = new SmartContractStateFactory(this.ContractPrimitiveSerializer, this.Network, this.InternalTxExecutorFactory);
        }
    }
}
