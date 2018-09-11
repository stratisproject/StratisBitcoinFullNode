using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class StateTransitionSpecification
    {
        private readonly IBlock block;
        private readonly SmartContractsRegTest network;
        private readonly IContractPrimitiveSerializer serializer;
        private readonly Mock<IInternalTransactionExecutorFactory> iteFactory;
        private readonly Mock<IContractState> trackedState;
        private readonly Mock<IContractStateRoot> contractStateRoot;
        private readonly Mock<IAddressGenerator> addressGenerator;
        private readonly Mock<ISmartContractVirtualMachine> vm;
        private readonly Mock<IContractState> trackedState2;

        public StateTransitionSpecification()
        {
            this.block = Mock.Of<IBlock>();
            this.network = new SmartContractsRegTest();

            this.serializer = Mock.Of<IContractPrimitiveSerializer>();
            this.iteFactory = new Mock<IInternalTransactionExecutorFactory>();
            this.trackedState = new Mock<IContractState>();
            this.contractStateRoot = new Mock<IContractStateRoot>();
            this.contractStateRoot.Setup(c => c.StartTracking())
                .Returns(this.trackedState.Object);
            this.trackedState2 = new Mock<IContractState>();
            this.trackedState.Setup(c => c.StartTracking())
                .Returns(this.trackedState2.Object);
            this.addressGenerator = new Mock<IAddressGenerator>();
            this.vm = new Mock<ISmartContractVirtualMachine>();
        }

        [Fact]
        public void ExternalCreate_Success()
        {
            // Preconditions checked:
            // - Has enough gas
            // Execution checked:
            // - Address generator is called with correct txhash and nonce
            // - Start tracking called on ContractStateRepo
            // - Create account is called on ContractStateRepo
            // - ITE factory is called
            // - Create is called on VM with correct args
            // - Gas remaining should be correct
            // - Commit called on (correct) state
            // - Result has correct values

            var transactionHash = new uint256();
            var expectedAddressGenerationNonce = 0UL;
            var newContractAddress = uint160.One;
            var gasLimit = (Gas)(GasPriceList.BaseCost + 100000);
            var vmExecutionResult = VmExecutionResult.Success(true, "Test");
            var externalCreateMessage = new ExternalCreateMessage(
                uint160.Zero,
                10,
                gasLimit,
                new byte[0],
                null
            );
            
            this.vm.Setup(v => v.Create(this.trackedState.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null))
                .Returns(vmExecutionResult);

            this.addressGenerator
                .Setup(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce))
                .Returns(newContractAddress);

            var state = new State(
                this.serializer,
                this.iteFactory.Object,
                this.vm.Object,
                this.contractStateRoot.Object,
                this.block,
                this.network,
                0,
                transactionHash,
                this.addressGenerator.Object,
                gasLimit
            );

            StateTransitionResult result = state.Apply(externalCreateMessage);

            this.addressGenerator.Verify(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce), Times.Once);

            this.contractStateRoot.Verify(sr => sr.StartTracking(), Times.Once);

            this.trackedState.Verify(ts => ts.CreateAccount(newContractAddress), Times.Once);

            this.vm.Verify(v => v.Create(this.trackedState.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null), Times.Once);

            this.trackedState.Verify(ts => ts.Commit(), Times.Once);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(this.trackedState.Object, state.GetPrivateFieldValue("intermediateState"));
            Assert.Equal(newContractAddress, result.Success.ContractAddress);
            Assert.Equal(vmExecutionResult.Result, result.Success.ExecutionResult);
            Assert.Equal(gasLimit - result.GasConsumed, state.GasRemaining);
            // In this test we only ever spend the base fee.
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void ExternalCreate_Nested_Create_Success()
        {
            var transactionHash = new uint256();
            var expectedAddressGenerationNonce = 0UL;
            var newContractAddress = uint160.One;
            var gasLimit = (Gas)(GasPriceList.BaseCost + 100000);
            var vmExecutionResult = VmExecutionResult.Success(true, "Test");
            var externalCreateMessage = new ExternalCreateMessage(
                uint160.Zero,
                10,
                gasLimit,
                new byte[0],
                null
            );

            var newContractAddress2 = new uint160(2);

            var internalCreateMessage = new InternalCreateMessage(
                newContractAddress,
                0,
                (Gas) (GasPriceList.BaseCost + 1000),
                null,
                "Test"
            );

            this.addressGenerator
                .Setup(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce))
                .Returns(newContractAddress);

            this.addressGenerator
                .Setup(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce + 1))
                .Returns(newContractAddress2);

            var vmExecutionResult2 = VmExecutionResult.Success(true, "NestedTest");

            var state = new State(this.serializer,
                this.iteFactory.Object, this.vm.Object,
                this.contractStateRoot.Object,
                this.block,
                this.network,
                0,
                transactionHash,
                this.addressGenerator.Object,
                gasLimit
            );

            // Setup the VM to invoke the state with a nested internal create
            // ReSharper disable once ArrangeThisQualifier
            this.vm.Setup(v => v.Create(this.trackedState.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null))
                .Callback(() => state.Apply(internalCreateMessage))
                .Returns(vmExecutionResult);

            // Setup the nested VM create result
            this.vm.Setup(v => v.Create(this.trackedState2.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code,
                    internalCreateMessage.Parameters, internalCreateMessage.Type))
                .Returns(vmExecutionResult2);

            StateTransitionResult result = state.Apply(externalCreateMessage);

            this.addressGenerator.Verify(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce), Times.Once);

            this.contractStateRoot.Verify(sr => sr.StartTracking(), Times.Once);

            this.trackedState.Verify(ts => ts.CreateAccount(newContractAddress), Times.Once);

            this.vm.Verify(v => v.Create(this.trackedState.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null), Times.Once);

            // Nesting begins here
            // The nested executor starts tracking on the parent state
            this.trackedState.Verify(ts => ts.StartTracking(), Times.Once);

            // Nested state transition generates a new address with the next nonce
            this.addressGenerator.Verify(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce + 1), Times.Once);

            // VM is called with all nested state params and the original code
            this.vm.Verify(v => v.Create(this.trackedState2.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, internalCreateMessage.Parameters, internalCreateMessage.Type), Times.Once);

            // The nested executor calls commit on the nested tracked state
            this.trackedState2.Verify(ts => ts.Commit(), Times.Once);

            Assert.Equal(1, state.InternalTransfers.Count);
            // TODO - It's a hack to need to test the internal state of the object like this.
            // We expect the intermediateState to be the last "committed to" state
            Assert.Equal(this.trackedState2.Object, state.GetPrivateFieldValue("intermediateState"));
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(gasLimit - result.GasConsumed, state.GasRemaining);

            // Two nested operations
            Assert.Equal(GasPriceList.BaseCost * 2, result.GasConsumed);
        }

        [Fact]
        public void ExternalCreate_Nested_Create_Failure()
        {
            var block = Mock.Of<IBlock>();
            var network = new SmartContractsRegTest();
            var transactionHash = new uint256();
            var expectedAddressGenerationNonce = 0UL;
            var newContractAddress = uint160.One;
            var newContractAddress2 = new uint160(2);

            var gasLimit = (Gas)(GasPriceList.BaseCost + 100000);

            var externalCreateMessage = new ExternalCreateMessage(
                uint160.Zero,
                10,
                gasLimit,
                new byte[0],
                null
            );

            var internalCreateMessage = new InternalCreateMessage(
                newContractAddress,
                0,
                (Gas)(GasPriceList.BaseCost + 1000),
                null,
                "Test"
            );

            this.addressGenerator
                .Setup(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce))
                .Returns(newContractAddress);

            this.addressGenerator
                .Setup(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce + 1))
                .Returns(newContractAddress2);

            var vmExecutionResult = VmExecutionResult.Success(true, "Test");
            var vmExecutionResult2 = VmExecutionResult.Error(new SmartContractAssertException("Error"));

            var vm = new Mock<ISmartContractVirtualMachine>(MockBehavior.Strict);

            var state = new State(
                this.serializer,
                this.iteFactory.Object,
                vm.Object,
                this.contractStateRoot.Object,
                block,
                network,
                0,
                transactionHash,
                this.addressGenerator.Object,
                gasLimit
            );

            // Setup the VM to invoke the state with a nested internal create
            vm.Setup(v => v.Create(this.trackedState.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code,
                    externalCreateMessage.Parameters, null))
                .Callback(() => state.Apply(internalCreateMessage))
                .Returns(vmExecutionResult);

            // Setup the nested VM create result
            vm.Setup(v => v.Create(this.trackedState2.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code,
                    internalCreateMessage.Parameters, internalCreateMessage.Type))
                .Returns(vmExecutionResult2);

            StateTransitionResult result = state.Apply(externalCreateMessage);

            this.addressGenerator.Verify(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce), Times.Once);

            this.contractStateRoot.Verify(sr => sr.StartTracking(), Times.Once);

            this.trackedState.Verify(ts => ts.CreateAccount(newContractAddress), Times.Once);

            vm.Verify(v => v.Create(this.trackedState.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null), Times.Once);

            // Nesting begins here
            // The nested executor starts tracking on the parent state
            this.trackedState.Verify(ts => ts.StartTracking(), Times.Once);

            // Nested state transition generates a new address with the next nonce
            this.addressGenerator.Verify(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce + 1), Times.Once);

            // VM is called with all nested state params and the original code
            vm.Verify(v => v.Create(this.trackedState2.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, internalCreateMessage.Parameters, internalCreateMessage.Type), Times.Once);

            // The nested executor never calls commit on the nested tracked state due to the error
            this.trackedState2.Verify(ts => ts.Commit(), Times.Never);

            Assert.Equal(0, state.InternalTransfers.Count);
            // We expect the intermediateState to be the last "committed to" state
            Assert.Equal(this.trackedState.Object, state.GetPrivateFieldValue("intermediateState"));

            // Even though the internal creation failed, the operation was still successful
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(gasLimit - result.GasConsumed, state.GasRemaining);

            // Two nested operations
            Assert.Equal(GasPriceList.BaseCost * 2, result.GasConsumed);
        }

        [Fact]
        public void ExternalCreate_Vm_Error()
        {
            var block = Mock.Of<IBlock>();
            var network = new SmartContractsRegTest();
            var transactionHash = new uint256();
            var expectedAddressGenerationNonce = 0UL;
            var newContractAddress = uint160.One;
            var gasLimit = (Gas)(GasPriceList.BaseCost + 100000);
            var vmExecutionResult = VmExecutionResult.Error(new SmartContractAssertException("Error"));

            var externalCreateMessage = new ExternalCreateMessage(
                uint160.Zero,
                10,
                gasLimit,
                new byte[0],
                null
            );

            this.addressGenerator
                .Setup(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce))
                .Returns(newContractAddress);

            this.vm.Setup(v => v.Create(this.trackedState.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code,
                    externalCreateMessage.Parameters, null))
                .Returns(vmExecutionResult);

            var state = new State(this.serializer,
                this.iteFactory.Object,
                this.vm.Object,
                this.contractStateRoot.Object,
                block,
                network,
                0,
                transactionHash,
                this.addressGenerator.Object,
                gasLimit
            );

            StateTransitionResult result = state.Apply(externalCreateMessage);

            this.addressGenerator.Verify(a => a.GenerateAddress(transactionHash, expectedAddressGenerationNonce), Times.Once);

            this.contractStateRoot.Verify(sr => sr.StartTracking(), Times.Once);

            this.trackedState.Verify(ts => ts.CreateAccount(newContractAddress), Times.Once);

            this.vm.Verify(v => v.Create(this.trackedState.Object, It.IsAny<ISmartContractState>(), externalCreateMessage.Code, externalCreateMessage.Parameters, null), Times.Once);

            this.trackedState.Verify(ts => ts.Commit(), Times.Never);

            // TODO - It's a hack to need to test the internal state of the object like this.
            Assert.Equal(this.contractStateRoot.Object, state.GetPrivateFieldValue("intermediateState"));
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFailure);
            Assert.NotNull(result.Error);
            Assert.Equal(vmExecutionResult.ExecutionException, result.Error.VmException);
            Assert.Equal(StateTransitionErrorKind.VmError, result.Error.Kind);
            Assert.Equal(gasLimit - result.GasConsumed, state.GasRemaining);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void ExternalCall_Success()
        {
            var transactionHash = new uint256();
            var gasLimit = (Gas)(GasPriceList.BaseCost + 100000);
            var vmExecutionResult = VmExecutionResult.Success(true, "Test");
            
            // Code must have a length to pass precondition checks.
            var code = new byte[1];
            var typeName = "Test";

            var externalCallMessage = new ExternalCallMessage(
                uint160.Zero,
                uint160.Zero,
                0,
                gasLimit,
                new MethodCall("Test", null)
            );

            this.trackedState
                .Setup(sr => sr.GetCode(externalCallMessage.To))
                .Returns(code);

            this.trackedState
                .Setup(sr => sr.GetContractType(externalCallMessage.To))
                .Returns(typeName);

            this.vm.Setup(v => 
                    v.ExecuteMethod(It.IsAny<ISmartContractState>(), externalCallMessage.Method, code, typeName))
                .Returns(vmExecutionResult);

            var state = new State(
                this.serializer,
                this.iteFactory.Object,
                this.vm.Object,
                this.contractStateRoot.Object,
                this.block,
                this.network,
                0,
                transactionHash,
                this.addressGenerator.Object,
                gasLimit
            );

            StateTransitionResult result = state.Apply(externalCallMessage);

            this.contractStateRoot.Verify(sr => sr.StartTracking(), Times.Once);

            this.trackedState.Verify(sr => sr.GetCode(externalCallMessage.To), Times.Once);

            this.trackedState.Verify(sr => sr.GetContractType(externalCallMessage.To), Times.Once);

            this.addressGenerator.Verify(a => a.GenerateAddress(It.IsAny<uint256>(), It.IsAny<ulong>()), Times.Never);

            this.trackedState.Verify(ts => ts.CreateAccount(It.IsAny<uint160>()), Times.Never);
            
            this.vm.Verify(
                v => v.ExecuteMethod(It.IsAny<ISmartContractState>(), externalCallMessage.Method, code, typeName),
                Times.Once);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Success);
            Assert.Equal(this.trackedState.Object, state.GetPrivateFieldValue("intermediateState"));
            Assert.Equal(externalCallMessage.To, result.Success.ContractAddress);
            Assert.Equal(vmExecutionResult.Result, result.Success.ExecutionResult);
            Assert.Equal(gasLimit - result.GasConsumed, state.GasRemaining);
            // In this test we only ever spend the base fee.
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }

        [Fact]
        public void ExternalCall_Vm_Error()
        {
            var transactionHash = new uint256();
            var newContractAddress = uint160.One;
            var gasLimit = (Gas)(GasPriceList.BaseCost + 100000);
            var vmExecutionResult = VmExecutionResult.Error(new SmartContractAssertException("Error"));

            // Code must have a length to pass precondition checks.
            var code = new byte[1];
            var typeName = "Test";

            var externalCallMessage = new ExternalCallMessage(
                uint160.Zero,
                uint160.Zero,
                0,
                gasLimit,
                new MethodCall("Test", null)
            );

            this.contractStateRoot
                .Setup(sr => sr.GetCode(externalCallMessage.To))
                .Returns(code);

            this.trackedState
                .Setup(sr => sr.GetContractType(externalCallMessage.To))
                .Returns(typeName);

            this.vm.Setup(v =>
                    v.ExecuteMethod(It.IsAny<ISmartContractState>(), externalCallMessage.Method, code, typeName))
                .Returns(vmExecutionResult);

            var state = new State(
                this.serializer,
                this.iteFactory.Object,
                this.vm.Object,
                this.contractStateRoot.Object,
                this.block,
                this.network,
                0,
                transactionHash,
                this.addressGenerator.Object,
                gasLimit
            );

            StateTransitionResult result = state.Apply(externalCallMessage);

            this.contractStateRoot.Verify(sr => sr.GetCode(externalCallMessage.To), Times.Once);

            this.contractStateRoot.Verify(sr => sr.StartTracking(), Times.Once);

            // Getting Type is called on the tracked state as it's possible
            // this also occurs on a nested call and the Type only exists
            // in the nested repository
            this.trackedState.Verify(sr => sr.GetContractType(externalCallMessage.To), Times.Once);

            this.addressGenerator.Verify(a => a.GenerateAddress(It.IsAny<uint256>(), It.IsAny<ulong>()), Times.Never);

            this.trackedState.Verify(ts => ts.CreateAccount(newContractAddress), Times.Never);

            this.vm.Verify(
                v => v.ExecuteMethod(It.IsAny<ISmartContractState>(), externalCallMessage.Method, code, typeName),
                Times.Once);

            this.trackedState.Verify(ts => ts.Commit(), Times.Never);

            Assert.True(result.IsFailure);
            Assert.NotNull(result.Error);
            Assert.Equal(this.contractStateRoot.Object, state.GetPrivateFieldValue("intermediateState"));
            Assert.Equal(result.Error.VmException, vmExecutionResult.ExecutionException);
            Assert.Equal(StateTransitionErrorKind.VmError, result.Error.Kind);
            Assert.Equal(gasLimit - result.GasConsumed, state.GasRemaining);
            Assert.Equal(GasPriceList.BaseCost, result.GasConsumed);
        }
    }
}