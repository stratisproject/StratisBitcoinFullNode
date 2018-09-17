using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ExecutorSpecification
    {
        [Fact]
        public void Create_Contract_Success()
        {
            var network = new SmartContractsRegTest();
            uint160 newContractAddress = uint160.One;
            var gasConsumed = (Gas) 100;
            var code = new byte[] {0xAA, 0xBB, 0xCC};
            var contractTxData = new ContractTxData(1, 1, (Gas) 1000, code);
            var refund = new Money(0);
            const ulong mempoolFee = 2UL; // MOQ doesn't like it when you use a type with implicit conversions (Money)
            ISmartContractTransactionContext context = Mock.Of<ISmartContractTransactionContext>(c => 
                c.Data == code &&
                c.MempoolFee == mempoolFee &&
                c.Sender == uint160.One &&
                c.CoinbaseAddress == uint160.Zero);

            var logger = new Mock<ILogger>();
            ILoggerFactory loggerFactory = Mock.Of<ILoggerFactory>
                    (l => l.CreateLogger(It.IsAny<string>()) == logger.Object);

            var callDataSerializer = new Mock<ICallDataSerializer>();            
            callDataSerializer
                .Setup(s => s.Deserialize(It.IsAny<byte[]>()))
                .Returns(Result.Ok(contractTxData));

            var contractPrimitiveSerializer = new Mock<IContractPrimitiveSerializer>();
            var vmExecutionResult = VmExecutionResult.Success(null, null);

            var contractStateRoot = new Mock<IContractState>();
            var transferProcessor = new Mock<ISmartContractResultTransferProcessor>();
            var stateProcessor = new Mock<IStateProcessor>();

            (Money refund, TxOut) refundResult = (refund, null);
            var refundProcessor = new Mock<ISmartContractResultRefundProcessor>();

            refundProcessor
                .Setup(r => r.Process(
                    contractTxData,
                    mempoolFee,
                    context.Sender,
                    It.IsAny<Gas>(),
                    false))
                .Returns(refundResult);

            var stateTransitionResult = StateTransitionResult.Ok(gasConsumed, newContractAddress, vmExecutionResult.Result);

            var internalTransfers = new List<TransferInfo>().AsReadOnly();

            var snapshot = new Mock<IState>();

            var stateMock = new Mock<IState>();
            stateMock.Setup(s => s.ContractState).Returns(contractStateRoot.Object);
            stateMock.SetupGet(p => p.InternalTransfers).Returns(internalTransfers);
            stateMock.Setup(s => s.Snapshot()).Returns(snapshot.Object);
            
            stateProcessor.Setup(s => s.Apply(snapshot.Object, It.IsAny<ExternalCreateMessage>())).Returns(stateTransitionResult);

            var stateFactory = new Mock<IStateFactory>();
            stateFactory.Setup(sf => sf.Create(
                contractStateRoot.Object,
                It.IsAny<IBlock>(),
                context.TxOutValue,
                context.TransactionHash))
            .Returns(stateMock.Object);

            var sut = new Executor(
                loggerFactory,
                callDataSerializer.Object,
                contractStateRoot.Object,
                refundProcessor.Object,
                transferProcessor.Object,
                network,
                stateFactory.Object,
                stateProcessor.Object,
                contractPrimitiveSerializer.Object);

            sut.Execute(context);

            callDataSerializer.Verify(s => s.Deserialize(code), Times.Once);
            
            stateFactory.Verify(sf => sf
                .Create(
                    contractStateRoot.Object,
                    It.IsAny<IBlock>(),
                    context.TxOutValue,
                    context.TransactionHash),
                Times.Once);

            // We only apply the message to the snapshot.
            stateProcessor.Verify(sm => sm.Apply(snapshot.Object, It.IsAny<ExternalCreateMessage>()), Times.Once);

            // Must transition to the snapshot.
            stateMock.Verify(sm => sm.TransitionTo(snapshot.Object), Times.Once);

            transferProcessor.Verify(t => t
                .Process(
                    contractStateRoot.Object, 
                    newContractAddress, 
                    context,
                    internalTransfers,
                    false), 
                Times.Once);

            refundProcessor.Verify(t => t
                    .Process(
                        contractTxData,
                        mempoolFee,
                        context.Sender,
                        It.IsAny<Gas>(),
                        false),
                Times.Once);
        }
    }
}
