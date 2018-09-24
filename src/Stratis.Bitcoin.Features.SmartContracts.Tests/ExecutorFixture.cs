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
using Stratis.SmartContracts.Executor.Reflection.ResultProcessors;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ExecutorFixture
    {
        public const ulong MempoolFee = 2UL; // MOQ doesn't like it when you use a type with implicit conversions (Money)
        public Money Refund = new Money(0);
        public byte[] Data = new byte[] { 0x11, 0x22, 0x33 };

        public ExecutorFixture(ContractTxData txData)
        {
            this.Network = new SmartContractsRegTest();

            this.ContractTransactionContext = Mock.Of<IContractTransactionContext>(c =>
                c.Data == this.Data &&
                c.MempoolFee == MempoolFee &&
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
            this.ContractStateRoot = new Mock<IStateRepository>();            
            this.TransferProcessor = new Mock<IContractTransferProcessor>();

            var refundProcessor = new Mock<IContractRefundProcessor>();
            refundProcessor
                .Setup(r => r.Process(
                    txData,
                    MempoolFee,
                    this.ContractTransactionContext.Sender,
                    It.IsAny<Gas>(),
                    It.IsAny<bool>()))
                .Returns((this.Refund, null));            
            this.RefundProcessor = refundProcessor;
            
            this.StateProcessor = new Mock<IStateProcessor>();

            this.State = new Mock<IState>();
            this.State.Setup(s => s.ContractState).Returns(this.ContractStateRoot.Object);
            this.State.SetupGet(p => p.InternalTransfers).Returns(new List<TransferInfo>().AsReadOnly());
            this.State.Setup(s => s.Snapshot()).Returns(Mock.Of<IState>());
            
            var stateFactory = new Mock<IStateFactory>();
            stateFactory.Setup(sf => sf.Create(
                    this.ContractStateRoot.Object,
                    It.IsAny<IBlock>(),
                    this.ContractTransactionContext.TxOutValue,
                    this.ContractTransactionContext.TransactionHash))
                .Returns(this.State.Object);
            this.StateFactory = stateFactory;
        }

        public Mock<IStateRepository> ContractStateRoot { get; }

        public Mock<IContractPrimitiveSerializer> ContractPrimitiveSerializer { get; }

        public Mock<IContractRefundProcessor> RefundProcessor { get; }

        public Mock<IContractTransferProcessor> TransferProcessor { get; }

        public Mock<IStateProcessor> StateProcessor { get; }

        public Mock<IStateFactory> StateFactory { get; }

        public Mock<IState> State { get; }

        public IContractTransactionContext ContractTransactionContext { get; }

        public Mock<ICallDataSerializer> CallDataSerializer { get; }

        public ILoggerFactory LoggerFactory { get; }

        public SmartContractsRegTest Network { get; }
    }
}