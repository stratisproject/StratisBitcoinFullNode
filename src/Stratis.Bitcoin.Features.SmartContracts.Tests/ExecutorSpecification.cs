using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ExecutorSpecification
    {
        [Fact]
        public void Create_Contract_Success()
        {
            var contractTxData = new ContractTxData(1, 1, (Gas) 1000, new byte[] { 0xAA, 0xBB, 0xCC });

            var vmExecutionResult = VmExecutionResult.Success(null, null);

            var stateTransitionResult = StateTransitionResult.Ok((Gas)100, uint160.One, vmExecutionResult.Result);

            var fixture = new ExecutorFixture(contractTxData);
            var snapshot = fixture.State.Object.Snapshot();

            fixture.StateProcessor
                .Setup(s => s.Apply(snapshot, It.IsAny<ExternalCreateMessage>()))
                .Returns(stateTransitionResult);

            var sut = new ContractExecutor(
                fixture.LoggerFactory,
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.RefundProcessor.Object,
                fixture.TransferProcessor.Object,
                fixture.Network,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            sut.Execute(fixture.ContractTransactionContext);

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
                    fixture.ContractStateRoot.Object, 
                    stateTransitionResult.Success.ContractAddress, 
                    fixture.ContractTransactionContext,
                    fixture.State.Object.InternalTransfers,
                    false), 
                Times.Once);

            fixture.RefundProcessor.Verify(t => t
                    .Process(
                        contractTxData,
                        ExecutorFixture.MempoolFee,
                        fixture.ContractTransactionContext.Sender,
                        It.IsAny<Gas>(),
                        false),
                Times.Once);
        }

        [Fact]
        public void Create_Contract_Failure()
        {
            var contractTxData = new ContractTxData(1, 1, (Gas)1000, new byte[] { 0xAA, 0xBB, 0xCC });
            
            var stateTransitionResult = StateTransitionResult.Fail((Gas) 100, new ContractErrorMessage("Error"));
            
            var fixture = new ExecutorFixture(contractTxData);
            var snapshot = fixture.State.Object.Snapshot();

            fixture.StateProcessor
                .Setup(s => s.Apply(snapshot, It.IsAny<ExternalCreateMessage>()))
                .Returns(stateTransitionResult);

            var sut = new ContractExecutor(
                fixture.LoggerFactory,
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.RefundProcessor.Object,
                fixture.TransferProcessor.Object,
                fixture.Network,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            sut.Execute(fixture.ContractTransactionContext);

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
                        fixture.ContractStateRoot.Object,
                        null,
                        fixture.ContractTransactionContext,
                        fixture.State.Object.InternalTransfers,
                        true),
                Times.Once);

            fixture.RefundProcessor.Verify(t => t
                    .Process(
                        contractTxData,
                        ExecutorFixture.MempoolFee,
                        fixture.ContractTransactionContext.Sender,
                        It.IsAny<Gas>(),
                        false),
                Times.Once);
        }

        [Fact]
        public void Call_Contract_Success()
        {
            var parameters = new object[] { };
            var contractTxData = new ContractTxData(1, 1, (Gas)1000, uint160.One, "TestMethod", "", parameters);

            var vmExecutionResult = VmExecutionResult.Success(null, null);

            var stateTransitionResult = StateTransitionResult.Ok((Gas)100, uint160.One, vmExecutionResult.Result);

            var fixture = new ExecutorFixture(contractTxData);
            var snapshot = fixture.State.Object.Snapshot();

            fixture.StateProcessor
                .Setup(s => s.Apply(snapshot, It.IsAny<ExternalCallMessage>()))
                .Returns(stateTransitionResult);

            var sut = new ContractExecutor(
                fixture.LoggerFactory,
                fixture.CallDataSerializer.Object,
                fixture.ContractStateRoot.Object,
                fixture.RefundProcessor.Object,
                fixture.TransferProcessor.Object,
                fixture.Network,
                fixture.StateFactory.Object,
                fixture.StateProcessor.Object,
                fixture.ContractPrimitiveSerializer.Object);

            sut.Execute(fixture.ContractTransactionContext);

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
                    fixture.ContractStateRoot.Object,
                    stateTransitionResult.Success.ContractAddress,
                    fixture.ContractTransactionContext,
                    fixture.State.Object.InternalTransfers,
                    false),
                Times.Once);

            fixture.RefundProcessor.Verify(t => t
                    .Process(
                        contractTxData,
                        ExecutorFixture.MempoolFee,
                        fixture.ContractTransactionContext.Sender,
                        It.IsAny<Gas>(),
                        false),
                Times.Once);
        }

    }
}
