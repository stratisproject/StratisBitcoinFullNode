using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
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
            uint160 newContractAddress = uint160.One;
            var gasConsumed = (Gas) 100;
            var code = new byte[] {0xAA, 0xBB, 0xCC};
            var contractTxData = new ContractTxData(1, 1, (Gas) 1000, code);
            var refund = new Money(0);
            const ulong mempoolFee = 2UL; // MOQ doesn't like it when you use a type with implicit conversions (Money)
            ISmartContractTransactionContext context = Mock.Of<ISmartContractTransactionContext>(c => 
                c.Data == code &&
                c.MempoolFee == mempoolFee &&
                c.Sender == uint160.One);

            var logger = new Mock<ILogger>();
            ILoggerFactory loggerFactory = Mock.Of<ILoggerFactory>
                    (l => l.CreateLogger(It.IsAny<string>()) == logger.Object);

            var serializer = new Mock<ICallDataSerializer>();            
            serializer
                .Setup(s => s.Deserialize(It.IsAny<byte[]>()))
                .Returns(Result.Ok(contractTxData));

            var contractPrimitiveSerializer = new Mock<IContractPrimitiveSerializer>();

            var vmExecutionResult =
                VmExecutionResult.CreationSuccess(
                    newContractAddress, 
                    new List<TransferInfo>(),
                    gasConsumed,
                    null,
                    null);

            var state = new Mock<IContractState>();
            var transferProcessor = new Mock<ISmartContractResultTransferProcessor>();

            (Money refund, List<TxOut>) refundResult = (refund, new List<TxOut>());
            var refundProcessor = new Mock<ISmartContractResultRefundProcessor>();
            refundProcessor
                .Setup(r => r.Process(
                    contractTxData,
                    mempoolFee,
                    context.Sender,
                    vmExecutionResult.GasConsumed,
                    vmExecutionResult.ExecutionException))
                .Returns(refundResult);

            var vm = new Mock<ISmartContractVirtualMachine>();
            vm.Setup(v => v.Create(It.Is<IGasMeter>(x => x.GasConsumed == GasPriceList.BaseCost),
                It.IsAny<IContractState>(),
                It.IsAny<ICreateData>(),
                It.IsAny<ITransactionContext>(),
                It.IsAny<string>()))
                .Returns(vmExecutionResult);

            var sut = new Executor(
                loggerFactory,
                contractPrimitiveSerializer.Object,
                serializer.Object,
                state.Object,
                refundProcessor.Object,
                transferProcessor.Object,
                vm.Object
            );

            sut.Execute(context);

            serializer.Verify(s => s.Deserialize(code), Times.Once);
            
            vm.Verify(v => 
                v.Create(
                    It.IsAny<IGasMeter>(), 
                    state.Object, 
                    contractTxData, 
                    It.IsAny<TransactionContext>(),
                    It.IsAny<string>()), 
                Times.Once);

            transferProcessor.Verify(t => t
                .Process(
                    state.Object, 
                    vmExecutionResult.NewContractAddress, 
                    It.IsAny<ISmartContractTransactionContext>(),
                    vmExecutionResult.InternalTransfers,
                    false), 
                Times.Once);

            refundProcessor.Verify(t => t
                    .Process(
                        contractTxData,
                        mempoolFee,
                        context.Sender,
                        vmExecutionResult.GasConsumed,
                        vmExecutionResult.ExecutionException),
                Times.Once);

            state.Verify(s => s.Commit(), Times.Once);
            state.Verify(s => s.Rollback(), Times.Never);
        }
    }
}
