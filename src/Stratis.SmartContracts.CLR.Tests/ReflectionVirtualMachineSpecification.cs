﻿using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.SmartContracts.CLR.Caching;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    /// <summary>
    /// Tests the interaction of components within the RVM.
    /// </summary>
    public class ReflectionVirtualMachineSpecification
    {
        private readonly ISmartContractValidator validator;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Mock<ILoader> loader;
        private readonly Mock<IContractModuleDefinitionReader> moduleDefReader;
        private readonly Mock<IContractAssemblyCache> assemblyCache;
        private readonly Address testAddress;
        private readonly IStateRepository stateRepository;

        public ReflectionVirtualMachineSpecification()
        {
            this.validator = Mock.Of<ISmartContractValidator>();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(c => c.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

            this.loader = new Mock<ILoader>();
            this.moduleDefReader = new Mock<IContractModuleDefinitionReader>();
            this.assemblyCache = new Mock<IContractAssemblyCache>();
            this.stateRepository = Mock.Of<IStateRepository>();
            this.testAddress = "0x0000000000000000000000000000000000000001".HexToAddress();
        }

        [Fact]
        public void Create_Contract_Sets_Observer()
        {
            var vm = new ReflectionVirtualMachine(this.validator, this.loggerFactory.Object, this.loader.Object, this.moduleDefReader.Object, this.assemblyCache.Object);

            var moduleDef = new Mock<IContractModuleDefinition>();
            moduleDef.Setup(m => m.ToByteCode()).Returns((ContractByteCode)new byte[] { });
            moduleDef.Setup(x => x.Validate(this.validator))
                .Returns(new SmartContractValidationResult(new List<ValidationResult>()));

            this.moduleDefReader.Setup(m => m.Read(It.IsAny<byte[]>())).Returns(Result.Ok(moduleDef.Object));

            var contractAssembly = new Mock<IContractAssembly>();
            contractAssembly.Setup(c => c.GetType(It.IsAny<string>())).Returns(typeof(string));

            // Set this false here to prevent further execution.
            contractAssembly.Setup(c => c.SetObserver(It.IsAny<Observer>())).Returns(false);

            this.loader.Setup(l => l.Load(It.IsAny<ContractByteCode>())).Returns(Result.Ok(contractAssembly.Object));

            this.assemblyCache.Setup(c => c.Retrieve(It.IsAny<uint256>())).Returns((CachedAssemblyPackage) null);

            var state = new SmartContractState(
                new Block(1, this.testAddress),
                new Message(this.testAddress, this.testAddress, 0),
                Mock.Of<IPersistentState>(),
                Mock.Of<ISerializer>(),
                Mock.Of<IContractLogHolder>(),
                Mock.Of<IInternalTransactionExecutor>(),
                new InternalHashHelper(),
                () => 1000);

            var gasMeter = Mock.Of<IGasMeter>();
            var result = vm.Create(this.stateRepository, state, gasMeter, new byte[] { }, new object[] { }, "Test");

            contractAssembly.Verify(c => c.SetObserver(It.Is<Observer>(g => g.GasMeter == gasMeter)), Times.Once);
        }

        [Fact]
        public void Call_Contract_Sets_Observer()
        {
            var vm = new ReflectionVirtualMachine(this.validator, this.loggerFactory.Object, this.loader.Object, this.moduleDefReader.Object, this.assemblyCache.Object);

            var moduleDef = new Mock<IContractModuleDefinition>();
            moduleDef.Setup(m => m.ToByteCode()).Returns((ContractByteCode)new byte[] { });

            this.moduleDefReader.Setup(m => m.Read(It.IsAny<byte[]>())).Returns(Result.Ok(moduleDef.Object));

            var contractAssembly = new Mock<IContractAssembly>();
            contractAssembly.Setup(c => c.GetType(It.IsAny<string>())).Returns(typeof(string));

            // Set this false here to prevent further execution.
            contractAssembly.Setup(c => c.SetObserver(It.IsAny<Observer>())).Returns(false);

            this.loader.Setup(l => l.Load(It.IsAny<ContractByteCode>())).Returns(Result.Ok(contractAssembly.Object));

            this.assemblyCache.Setup(c => c.Retrieve(It.IsAny<uint256>())).Returns((CachedAssemblyPackage)null);
            
            var state = new SmartContractState(
                new Block(1, this.testAddress),
                new Message(this.testAddress, this.testAddress, 0),
                Mock.Of<IPersistentState>(),
                Mock.Of<ISerializer>(),
                Mock.Of<IContractLogHolder>(),
                Mock.Of<IInternalTransactionExecutor>(),
                new InternalHashHelper(),
                () => 1000);

            var gasMeter = Mock.Of<IGasMeter>();
            var executionContext = new ExecutionContext(new Observer(gasMeter, new MemoryMeter(100_000)));
            var result = vm.ExecuteMethod(state, executionContext, new MethodCall("Test"), new byte[] { }, "");

            contractAssembly.Verify(c => c.SetObserver(It.Is<Observer>(g => g.GasMeter == gasMeter)), Times.Once);
        }
    }
}