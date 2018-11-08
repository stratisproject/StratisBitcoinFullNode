using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Local;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class LocalExecutorSpecification
    {
        [Fact]
        public void Create_Contract_Success()
        {
            var contractTxData = new ContractTxData(1, 1, (Gas)1000, new byte[] { 0xAA, 0xBB, 0xCC });

            VmExecutionResult vmExecutionResult = VmExecutionResult.Ok(new object(), null);

            StateTransitionResult stateTransitionResult = StateTransitionResult.Ok((Gas)100, uint160.One, vmExecutionResult.Success.Result);

            var fixture = new ExecutorFixture(contractTxData);
            IState snapshot = fixture.State.Object.Snapshot();

            fixture.StateProcessor
                .Setup(s => s.Apply(snapshot, It.IsAny<ExternalCreateMessage>()))
                .Returns(stateTransitionResult);

            IStateRepository trackedMock = Mock.Of<IStateRepository>();
            fixture.ContractStateRoot.Setup(s => s.StartTracking()).Returns(trackedMock);

            var sut = new LocalExecutor(
                fixture.LoggerFactory,
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            ILocalExecutionResult result = sut.Execute(fixture.ContractTransactionContext);

            fixture.CallDataSerializer.Verify(s => s.Deserialize(fixture.ContractTransactionContext.Data), Times.Once);

            fixture.StateFactory.Verify(sf => sf
                .Create(
                    trackedMock,
                    It.IsAny<IBlock>(),
                    fixture.ContractTransactionContext.TxOutValue,
                    fixture.ContractTransactionContext.TransactionHash),
                Times.Once);

            // We only apply the message to the snapshot.
            fixture.StateProcessor.Verify(sm => sm.Apply(snapshot, It.IsAny<ExternalCreateMessage>()), Times.Once);

            // Should never transition to the snapshot.
            fixture.State.Verify(sm => sm.TransitionTo(snapshot), Times.Never);

            // Should never save on the state
            fixture.ContractStateRoot.Verify(sr => sr.Commit(), Times.Never);

            Assert.Null(result.ErrorMessage);
            Assert.False(result.Revert);
            Assert.Equal(fixture.State.Object.InternalTransfers, result.InternalTransfers);
            Assert.Equal(stateTransitionResult.GasConsumed, result.GasConsumed);
            Assert.Equal(stateTransitionResult.Success.ExecutionResult, result.Return);
            Assert.Equal(fixture.State.Object.GetLogs(fixture.ContractPrimitiveSerializer.Object), result.Logs);
        }

        [Fact]
        public void Create_Contract_Failure()
        {
            var contractTxData = new ContractTxData(1, 1, (Gas)1000, new byte[] { 0xAA, 0xBB, 0xCC });

            StateTransitionResult stateTransitionResult = StateTransitionResult.Fail((Gas)100, StateTransitionErrorKind.VmError);

            var fixture = new ExecutorFixture(contractTxData);
            IState snapshot = fixture.State.Object.Snapshot();

            fixture.StateProcessor
                .Setup(s => s.Apply(snapshot, It.IsAny<ExternalCreateMessage>()))
                .Returns(stateTransitionResult);

            IStateRepository trackedMock = Mock.Of<IStateRepository>();
            fixture.ContractStateRoot.Setup(s => s.StartTracking()).Returns(trackedMock);

            var sut = new LocalExecutor(
                fixture.LoggerFactory,
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            ILocalExecutionResult result = sut.Execute(fixture.ContractTransactionContext);

            fixture.CallDataSerializer.Verify(s => s.Deserialize(fixture.Data), Times.Once);

            fixture.StateFactory.Verify(sf => sf
                    .Create(
                        trackedMock,
                        It.IsAny<IBlock>(),
                        fixture.ContractTransactionContext.TxOutValue,
                        fixture.ContractTransactionContext.TransactionHash),
                Times.Once);

            // We only apply the message to the snapshot.
            fixture.StateProcessor.Verify(sm => sm.Apply(fixture.State.Object.Snapshot(),
                It.Is<ExternalCreateMessage>(m =>
                    m.Code == contractTxData.ContractExecutionCode
                    && m.Parameters == contractTxData.MethodParameters)), Times.Once);

            // Should never transition to the snapshot.
            fixture.State.Verify(sm => sm.TransitionTo(fixture.State.Object.Snapshot()), Times.Never);

            // Should never save on the state
            fixture.ContractStateRoot.Verify(sr => sr.Commit(), Times.Never);

            Assert.Equal(stateTransitionResult.Error.VmError, result.ErrorMessage);
            Assert.True(result.Revert);
            Assert.Equal(fixture.State.Object.InternalTransfers, result.InternalTransfers);
            Assert.Equal(stateTransitionResult.GasConsumed, result.GasConsumed);
            Assert.Null(result.Return);
            Assert.Equal(fixture.State.Object.GetLogs(fixture.ContractPrimitiveSerializer.Object), result.Logs);
        }

        [Fact]
        public void Call_Contract_Success()
        {
            var parameters = new object[] { };
            var contractTxData = new ContractTxData(1, 1, (Gas)1000, uint160.One, "TestMethod", parameters);

            VmExecutionResult vmExecutionResult = VmExecutionResult.Ok(new object(), null);

            StateTransitionResult stateTransitionResult = StateTransitionResult.Ok((Gas)100, uint160.One, vmExecutionResult.Success.Result);

            var fixture = new ExecutorFixture(contractTxData);
            IState snapshot = fixture.State.Object.Snapshot();

            fixture.StateProcessor
                .Setup(s => s.Apply(snapshot, It.IsAny<ExternalCallMessage>()))
                .Returns(stateTransitionResult);

            IStateRepository trackedMock = Mock.Of<IStateRepository>();
            fixture.ContractStateRoot.Setup(s => s.StartTracking()).Returns(trackedMock);

            var sut = new LocalExecutor(
                fixture.LoggerFactory,
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            ILocalExecutionResult result = sut.Execute(fixture.ContractTransactionContext);

            fixture.CallDataSerializer.Verify(s => s.Deserialize(fixture.ContractTransactionContext.Data), Times.Once);

            // Local executor used a tracked staterepository
            fixture.StateFactory.Verify(sf => sf
                .Create(
                    trackedMock,
                    It.IsAny<IBlock>(),
                    fixture.ContractTransactionContext.TxOutValue,
                    fixture.ContractTransactionContext.TransactionHash),
                Times.Once);

            // We only apply the message to the snapshot.
            fixture.StateProcessor.Verify(sm => sm.Apply(snapshot, It.Is<ExternalCallMessage>(m =>
                m.Method.Name == contractTxData.MethodName
                && m.Method.Parameters == contractTxData.MethodParameters
                && m.Amount == fixture.ContractTransactionContext.TxOutValue
                && m.From == fixture.ContractTransactionContext.Sender
                && m.To == contractTxData.ContractAddress)), Times.Once);

            // Should never transition to the snapshot.
            fixture.State.Verify(sm => sm.TransitionTo(snapshot), Times.Never);

            // Should never save on the state
            fixture.ContractStateRoot.Verify(sr => sr.Commit(), Times.Never);

            Assert.Null(result.ErrorMessage);
            Assert.False(result.Revert);
            Assert.Equal(fixture.State.Object.InternalTransfers, result.InternalTransfers);
            Assert.Equal(stateTransitionResult.GasConsumed, result.GasConsumed);
            Assert.Equal(stateTransitionResult.Success.ExecutionResult, result.Return);
            Assert.Equal(fixture.State.Object.GetLogs(fixture.ContractPrimitiveSerializer.Object), result.Logs);
        }

        [Fact]
        public void Call_Contract_Failure()
        {
            var parameters = new object[] { };
            var contractTxData = new ContractTxData(1, 1, (Gas)1000, uint160.One, "TestMethod", parameters);

            StateTransitionResult stateTransitionResult = StateTransitionResult.Fail((Gas)100, StateTransitionErrorKind.VmError);

            var fixture = new ExecutorFixture(contractTxData);
            IState snapshot = fixture.State.Object.Snapshot();

            fixture.StateProcessor
                .Setup(s => s.Apply(snapshot, It.IsAny<ExternalCallMessage>()))
                .Returns(stateTransitionResult);

            IStateRepository trackedMock = Mock.Of<IStateRepository>();
            fixture.ContractStateRoot.Setup(s => s.StartTracking()).Returns(trackedMock);
        
            var sut = new LocalExecutor(
                fixture.LoggerFactory,
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            ILocalExecutionResult result = sut.Execute(fixture.ContractTransactionContext);

            fixture.CallDataSerializer.Verify(s => s.Deserialize(fixture.Data), Times.Once);

            fixture.StateFactory.Verify(sf => sf
                    .Create(
                        trackedMock,
                        It.IsAny<IBlock>(),
                        fixture.ContractTransactionContext.TxOutValue,
                        fixture.ContractTransactionContext.TransactionHash),
                Times.Once);

            // We only apply the message to the snapshot.
            fixture.StateProcessor.Verify(sm => sm.Apply(snapshot, It.Is<ExternalCallMessage>(m =>
                m.Method.Name == contractTxData.MethodName
                && m.Method.Parameters == contractTxData.MethodParameters
                && m.Amount == fixture.ContractTransactionContext.TxOutValue
                && m.From == fixture.ContractTransactionContext.Sender
                && m.To == contractTxData.ContractAddress)), Times.Once);
            
            // Should never transition to the snapshot.
            fixture.State.Verify(sm => sm.TransitionTo(snapshot), Times.Never);

            // Should never save on the state
            fixture.ContractStateRoot.Verify(sr => sr.Commit(), Times.Never);

            Assert.Equal(stateTransitionResult.Error.VmError, result.ErrorMessage);
            Assert.True(result.Revert);
            Assert.Equal(fixture.State.Object.InternalTransfers, result.InternalTransfers);
            Assert.Equal(stateTransitionResult.GasConsumed, result.GasConsumed);
            Assert.Null(result.Return);
            Assert.Equal(fixture.State.Object.GetLogs(fixture.ContractPrimitiveSerializer.Object), result.Logs);
        }
    }
}
