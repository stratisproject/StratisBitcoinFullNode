using Moq;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ExecutorSpecification
    {
        [Fact]
        public void Create_Contract_Success()
        {
            var contractTxData = new ContractTxData(1, 1, (Gas) 1000, new byte[] { 0xAA, 0xBB, 0xCC });

            VmExecutionResult vmExecutionResult = VmExecutionResult.Ok(new object(), null);

            StateTransitionResult stateTransitionResult = StateTransitionResult.Ok((Gas)100, uint160.One, vmExecutionResult.Success.Result);

            var fixture = new ExecutorFixture(contractTxData);
            IState snapshot = fixture.State.Object.Snapshot();

            fixture.StateProcessor
                .Setup(s => s.Apply(snapshot, It.IsAny<ExternalCreateMessage>()))
                .Returns(stateTransitionResult);

            var sut = new ContractExecutor(
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.RefundProcessor.Object,
                fixture.TransferProcessor.Object,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            IContractExecutionResult result = sut.Execute(fixture.ContractTransactionContext);

            fixture.CallDataSerializer.Verify(s => s.Deserialize(fixture.ContractTransactionContext.Data), Times.Once);
            
            fixture.StateFactory.Verify(sf => sf
                .Create(
                    fixture.ContractStateRoot.Object,
                    It.IsAny<IBlock>(),
                    fixture.ContractTransactionContext.TxOutValue,
                    fixture.ContractTransactionContext.TransactionHash),
                Times.Once);

            // We only apply the message to the snapshot.
            fixture.StateProcessor.Verify(sm => sm.Apply(snapshot, It.IsAny<ExternalCreateMessage>()), Times.Once);

            // Must transition to the snapshot.
            fixture.State.Verify(sm => sm.TransitionTo(snapshot), Times.Once);

            fixture.TransferProcessor.Verify(t => t
                .Process(
                    snapshot.ContractState, 
                    stateTransitionResult.Success.ContractAddress, 
                    fixture.ContractTransactionContext,
                    snapshot.InternalTransfers,
                    false), 
                Times.Once);

            fixture.RefundProcessor.Verify(t => t
                    .Process(
                        contractTxData,
                        fixture.MempoolFee,
                        fixture.ContractTransactionContext.Sender,
                        It.IsAny<Gas>(),
                        false),
                Times.Once);

            Assert.Null(result.To);
            Assert.Equal(stateTransitionResult.Success.ContractAddress, result.NewContractAddress);
            Assert.Null(result.ErrorMessage);
            Assert.False(result.Revert);
            Assert.Equal(stateTransitionResult.GasConsumed, result.GasConsumed);
            Assert.Equal(stateTransitionResult.Success.ExecutionResult, result.Return);
            Assert.Equal(fixture.InternalTransaction, result.InternalTransaction);
            Assert.Equal(fixture.Fee, (Money)result.Fee);
            Assert.Equal(fixture.Refund, result.Refund);
            Assert.Equal(fixture.State.Object.GetLogs(fixture.ContractPrimitiveSerializer.Object), result.Logs);
        }

        [Fact]
        public void Create_Contract_Failure()
        {
            var contractTxData = new ContractTxData(1, 1, (Gas)1000, new byte[] { 0xAA, 0xBB, 0xCC });
            
            StateTransitionResult stateTransitionResult = StateTransitionResult.Fail((Gas) 100, StateTransitionErrorKind.VmError);
            
            var fixture = new ExecutorFixture(contractTxData);
            IState snapshot = fixture.State.Object.Snapshot();

            fixture.StateProcessor
                .Setup(s => s.Apply(snapshot, It.IsAny<ExternalCreateMessage>()))
                .Returns(stateTransitionResult);

            var sut = new ContractExecutor(
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.RefundProcessor.Object,
                fixture.TransferProcessor.Object,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            IContractExecutionResult result = sut.Execute(fixture.ContractTransactionContext);

            fixture.CallDataSerializer.Verify(s => s.Deserialize(fixture.Data), Times.Once);

            fixture.StateFactory.Verify(sf => sf
                    .Create(
                        fixture.ContractStateRoot.Object,
                        It.IsAny<IBlock>(),
                        fixture.ContractTransactionContext.TxOutValue,
                        fixture.ContractTransactionContext.TransactionHash),
                Times.Once);

            // We only apply the message to the snapshot.
            fixture.StateProcessor.Verify(sm => sm.Apply(fixture.State.Object.Snapshot(),
                It.Is<ExternalCreateMessage>(m =>
                    m.Code == contractTxData.ContractExecutionCode
                    && m.Parameters == contractTxData.MethodParameters)), Times.Once);

            // We do not transition to the snapshot because the applying the message was unsuccessful.
            fixture.State.Verify(sm => sm.TransitionTo(fixture.State.Object.Snapshot()), Times.Never);

            fixture.TransferProcessor.Verify(t => t
                    .Process(
                        snapshot.ContractState,
                        null,
                        fixture.ContractTransactionContext,
                        snapshot.InternalTransfers,
                        true),
                Times.Once);

            fixture.RefundProcessor.Verify(t => t
                    .Process(
                        contractTxData,
                        fixture.MempoolFee,
                        fixture.ContractTransactionContext.Sender,
                        It.IsAny<Gas>(),
                        false),
                Times.Once);

            Assert.Null(result.To);
            Assert.Null(result.NewContractAddress);
            Assert.Equal(stateTransitionResult.Error.VmError, result.ErrorMessage);
            Assert.True(result.Revert);
            Assert.Equal(stateTransitionResult.GasConsumed, result.GasConsumed);
            Assert.Null(result.Return);
            Assert.Equal(fixture.InternalTransaction, result.InternalTransaction);
            Assert.Equal(fixture.Fee, (Money)result.Fee);
            Assert.Equal(fixture.Refund, result.Refund);
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

            var sut = new ContractExecutor(
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.RefundProcessor.Object,
                fixture.TransferProcessor.Object,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            IContractExecutionResult result = sut.Execute(fixture.ContractTransactionContext);

            fixture.CallDataSerializer.Verify(s => s.Deserialize(fixture.ContractTransactionContext.Data), Times.Once);

            fixture.StateFactory.Verify(sf => sf
                .Create(
                    fixture.ContractStateRoot.Object,
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

            // Must transition to the snapshot.
            fixture.State.Verify(sm => sm.TransitionTo(snapshot), Times.Once);

            fixture.TransferProcessor.Verify(t => t
                .Process(
                    snapshot.ContractState,
                    stateTransitionResult.Success.ContractAddress,
                    fixture.ContractTransactionContext,
                    snapshot.InternalTransfers,
                    false),
                Times.Once);

            fixture.RefundProcessor.Verify(t => t
                    .Process(
                        contractTxData,
                        fixture.MempoolFee,
                        fixture.ContractTransactionContext.Sender,
                        It.IsAny<Gas>(),
                        false),
                Times.Once);

            Assert.Equal(contractTxData.ContractAddress, result.To);
            Assert.Null(result.NewContractAddress);
            Assert.Null(result.ErrorMessage);
            Assert.False(result.Revert);
            Assert.Equal(stateTransitionResult.GasConsumed, result.GasConsumed);
            Assert.Equal(stateTransitionResult.Success.ExecutionResult, result.Return);
            Assert.Equal(fixture.InternalTransaction, result.InternalTransaction);
            Assert.Equal(fixture.Fee, (Money)result.Fee);
            Assert.Equal(fixture.Refund, result.Refund);
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

            var sut = new ContractExecutor(
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.RefundProcessor.Object,
                fixture.TransferProcessor.Object,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            IContractExecutionResult result = sut.Execute(fixture.ContractTransactionContext);

            fixture.CallDataSerializer.Verify(s => s.Deserialize(fixture.Data), Times.Once);

            fixture.StateFactory.Verify(sf => sf
                    .Create(
                        fixture.ContractStateRoot.Object,
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

            // We do not transition to the snapshot because the applying the message was unsuccessful.
            fixture.State.Verify(sm => sm.TransitionTo(snapshot), Times.Never);

            // Transfer processor is called with null for new contract address and true for reversion required.
            fixture.TransferProcessor.Verify(t => t
                    .Process(
                        snapshot.ContractState,
                        null,
                        fixture.ContractTransactionContext,
                        snapshot.InternalTransfers,
                        true),
                Times.Once);

            fixture.RefundProcessor.Verify(t => t
                    .Process(
                        contractTxData,
                        fixture.MempoolFee,
                        fixture.ContractTransactionContext.Sender,
                        stateTransitionResult.GasConsumed,
                        false),
                Times.Once);

            Assert.Equal(contractTxData.ContractAddress, result.To);
            Assert.Null(result.NewContractAddress);
            Assert.Equal(stateTransitionResult.Error.VmError, result.ErrorMessage);
            Assert.True(result.Revert);
            Assert.Equal(stateTransitionResult.GasConsumed, result.GasConsumed);
            Assert.Null(result.Return);
            Assert.Equal(fixture.InternalTransaction, result.InternalTransaction);
            Assert.Equal(fixture.Fee, (Money) result.Fee);
            Assert.Equal(fixture.Refund, result.Refund);
            Assert.Equal(fixture.State.Object.GetLogs(fixture.ContractPrimitiveSerializer.Object), result.Logs);
        }
    }
}
