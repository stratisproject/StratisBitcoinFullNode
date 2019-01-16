using System;
using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class StateTests
    {
        private readonly Mock<IStateRepositoryRoot> contractStateRoot;
        private readonly Mock<IStateRepository> trackedState;
        private readonly Mock<IContractLogHolder> contractLogHolder;

        public StateTests()
        {
            this.trackedState = new Mock<IStateRepository>();
            this.contractStateRoot = new Mock<IStateRepositoryRoot>();
            this.contractStateRoot.Setup(c => c.StartTracking())
                .Returns(this.trackedState.Object);
            this.contractLogHolder = new Mock<IContractLogHolder>();
            this.contractLogHolder.Setup(l => l.GetRawLogs()).Returns((IList<RawLog>) new List<RawLog>());
        }

        [Fact]
        public void State_Snapshot_Uses_Tracked_ContractState()
        {
            var state = new State(null, this.contractStateRoot.Object, this.contractLogHolder.Object, new List<TransferInfo>(), null, null);

            IState newState = state.Snapshot();

            this.contractStateRoot.Verify(s => s.StartTracking(), Times.Once);

            Assert.NotSame(newState.ContractState, state.ContractState);
        }

        [Fact]
        public void State_Snapshot_Has_New_LogHolder_With_Original_Logs()
        {
            this.contractLogHolder.Setup(l => l.GetRawLogs())
                .Returns(new List<RawLog>
                {
                    new RawLog(null, null),
                    new RawLog(null, null),
                    new RawLog(null, null),
                    new RawLog(null, null)
                });

            var state = new State(null, this.contractStateRoot.Object, this.contractLogHolder.Object, new List<TransferInfo>(), null, null);

            IState newState = state.Snapshot();

            Assert.NotSame(newState.LogHolder, state.LogHolder);

            var newLogs = newState.LogHolder.GetRawLogs();

            foreach (RawLog log in state.LogHolder.GetRawLogs())
            {
                Assert.Contains(log, newLogs);
            }
        }

        [Fact]
        public void State_Snapshot_Has_New_InternalTransfers_With_Original_Transfers()
        {
            var transfers = new List<TransferInfo>
            {
                new TransferInfo(null, null, 0),
                new TransferInfo(null, null, 0)
            };

            var state = new State(null, this.contractStateRoot.Object, this.contractLogHolder.Object, transfers, null, null);

            IState newState = state.Snapshot();

            Assert.NotSame(newState.InternalTransfers, state.InternalTransfers);

            var newInternalTransfers = newState.InternalTransfers;

            foreach (var transfer in state.InternalTransfers)
            {
                Assert.Contains(transfer, newInternalTransfers);
            }
        }

        [Fact]
        public void State_Snapshot_Has_New_BalanceState()
        {
            var state = new State(null, this.contractStateRoot.Object, this.contractLogHolder.Object, new List<TransferInfo>(), null, null);

            IState newState = state.Snapshot();

            Assert.NotSame(state.BalanceState, newState.BalanceState);
        }

        [Fact]
        public void State_Snapshot_BalanceState_Has_Original_InitialBalance()
        {
            var state = new State(null, this.contractStateRoot.Object, this.contractLogHolder.Object, new List<TransferInfo>(), null, null);

            ulong initialBalance = 123456;
            uint160 initialAddress = uint160.One;
            state.AddInitialTransfer(new TransferInfo(uint160.Zero, initialAddress, initialBalance));

            IState newState = state.Snapshot();

            Assert.Equal(initialBalance, state.BalanceState.InitialTransfer.Value);
            Assert.Equal(initialAddress, state.BalanceState.InitialTransfer.To);
            Assert.Same(state.BalanceState.InitialTransfer, newState.BalanceState.InitialTransfer);
            Assert.Equal(state.BalanceState.InitialTransfer, newState.BalanceState.InitialTransfer);
        }

        [Fact]
        public void State_Snapshot_Has_Original_NonceGenerator()
        {
            var state = new State(null, this.contractStateRoot.Object, this.contractLogHolder.Object, new List<TransferInfo>(), null, null);

            IState newState = state.Snapshot();

            Assert.Same(state.NonceGenerator, newState.NonceGenerator);
        }

        [Fact]
        public void TransitionTo_Fails_If_New_State_Is_Not_Child()
        {
            var state = new State(null, this.contractStateRoot.Object, this.contractLogHolder.Object, new List<TransferInfo>(), null, null);

            IState newState = state.Snapshot();

            IState newState2 = newState.Snapshot();

            Assert.Throws<ArgumentException>(() => state.TransitionTo(newState2));
        }

        [Fact]
        public void TransitionTo_Updates_State_Correctly()
        {
            var state = new State(null, this.contractStateRoot.Object, this.contractLogHolder.Object, new List<TransferInfo>(), null, null);

            var newTransfers = new List<TransferInfo>
            {
                new TransferInfo(null, null, 0),
                new TransferInfo(null, null, 0),
                new TransferInfo(null, null, 0)
            };

            var newLogs = new List<RawLog>
            {
                new RawLog(null, null),
                new RawLog(null, null)
            };

            var testLogHolder = new Mock<IContractLogHolder>();
            testLogHolder.Setup(lh => lh.GetRawLogs())
                .Returns(newLogs);

            var testState = new Mock<IState>();
            testState.SetupGet(ts => ts.InternalTransfers)
                .Returns(newTransfers);
            testState.Setup(ts => ts.LogHolder)
                .Returns(testLogHolder.Object);
            testState.Setup(ts => ts.ContractState)
                .Returns(this.trackedState.Object);

            state.SetPrivateFieldValue("child", testState.Object);

            state.TransitionTo(testState.Object);

            this.trackedState.Verify(s => s.Commit(), Times.Once);

            this.contractLogHolder.Verify(l => l.Clear(), Times.Once);
            this.contractLogHolder.Verify(l => l.AddRawLogs(newLogs), Times.Once);

            Assert.Equal(newTransfers.Count, state.InternalTransfers.Count);
            Assert.Contains(newTransfers[0], state.InternalTransfers);
            Assert.Contains(newTransfers[1], state.InternalTransfers);
            Assert.Contains(newTransfers[2], state.InternalTransfers);
        }

        [Fact]
        public void New_State_NonceGenerator_Generates_Zero_Nonce()
        {
            var state = new State(null, this.contractStateRoot.Object, this.contractLogHolder.Object, new List<TransferInfo>(), null, null);
            
            Assert.Equal(0UL, state.NonceGenerator.Next);
        }
    }
}