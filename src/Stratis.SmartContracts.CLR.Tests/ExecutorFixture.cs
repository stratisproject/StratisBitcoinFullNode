using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.SmartContracts.CLR.ResultProcessors;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ExecutorFixture
    {
        public ulong MempoolFee { get; } = 2UL;
        public Money Fee { get; } = new Money(0);
        public TxOut Refund { get; } = new TxOut(Money.Zero, new Script());
        public byte[] Data { get; } = new byte[] {0x11, 0x22, 0x33};
        public Transaction InternalTransaction { get; } = new Transaction();

        public ExecutorFixture(ContractTxData txData)
        {
            this.ContractTransactionContext = Mock.Of<IContractTransactionContext>(c =>
                c.Data == this.Data &&
                c.MempoolFee == this.MempoolFee &&
                c.Sender == uint160.One &&
                c.CoinbaseAddress == uint160.Zero);

            var logger = new Mock<ILogger>();
            this.LoggerFactory = Mock.Of<ILoggerFactory>
                (l => l.CreateLogger(It.IsAny<string>()) == logger.Object);

            var callDataSerializer = new Mock<ICallDataSerializer>();
            callDataSerializer
                .Setup(s => s.Deserialize(It.IsAny<byte[]>()))
                .Returns(Result.Ok(txData));
            this.CallDataSerializer = callDataSerializer;

            this.ContractPrimitiveSerializer = new Mock<IContractPrimitiveSerializer>();
            this.ContractStateRoot = new Mock<IStateRepositoryRoot>();

            var transferProcessor = new Mock<IContractTransferProcessor>();
            transferProcessor.Setup<Transaction>(t => t.Process(
                    It.IsAny<IStateRepository>(),
                    It.IsAny<uint160>(),
                    It.IsAny<IContractTransactionContext>(),
                    It.IsAny<IReadOnlyList<TransferInfo>>(),
                    It.IsAny<bool>()
                ))
                .Returns(this.InternalTransaction);
            this.TransferProcessor = transferProcessor;

            var refundProcessor = new Mock<IContractRefundProcessor>();
            refundProcessor
                .Setup(r => r.Process(
                    It.IsAny<ContractTxData>(),
                    It.IsAny<ulong>(),
                    It.IsAny<uint160>(),
                    It.IsAny<Gas>(),
                    It.IsAny<bool>()))
                .Returns((this.Fee, this.Refund));            
            this.RefundProcessor = refundProcessor;
            
            this.StateProcessor = new Mock<IStateProcessor>();

            var state = new Mock<IState>();
            state.Setup(s => s.ContractState).Returns(this.ContractStateRoot.Object);
            state.SetupGet(p => p.InternalTransfers).Returns((IReadOnlyList<TransferInfo>) new List<TransferInfo>().AsReadOnly());
            var snapshot = new Mock<IState>();
            snapshot.SetupGet(p => p.InternalTransfers).Returns(new List<TransferInfo>().AsReadOnly());
            snapshot.Setup(s => s.ContractState).Returns(Mock.Of<IStateRepository>());
            state.Setup(s => s.Snapshot()).Returns(snapshot.Object);
            state.Setup(s => s.GetLogs(this.ContractPrimitiveSerializer.Object))
                .Returns(new List<Log>());
            this.State = state;

            var stateFactory = new Mock<IStateFactory>();
            stateFactory.Setup(sf => sf.Create(
                    It.IsAny<IStateRepository>(),
                    It.IsAny<IBlock>(),
                    It.IsAny<ulong>(),
                    It.IsAny<uint256>()))
                .Returns(this.State.Object);
            this.StateFactory = stateFactory;
        }

        public Mock<IStateRepositoryRoot> ContractStateRoot { get; }

        public Mock<IContractPrimitiveSerializer> ContractPrimitiveSerializer { get; }

        public Mock<IContractRefundProcessor> RefundProcessor { get; }

        public Mock<IContractTransferProcessor> TransferProcessor { get; }

        public Mock<IStateProcessor> StateProcessor { get; }

        public Mock<IStateFactory> StateFactory { get; }

        public Mock<IState> State { get; }

        public IContractTransactionContext ContractTransactionContext { get; }

        public Mock<ICallDataSerializer> CallDataSerializer { get; }

        public ILoggerFactory LoggerFactory { get; }
    }
}