﻿using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class InternalExecutorSpecification
    {
        [Fact]
        public void Create_StateTransition_Success()
        {
            ulong amount = 100UL;
            var parameters = new object[] { };
            var gasLimit = (Gas)100_000;

            var fixture = new InternalExecutorTestFixture();
            
            fixture.SetGasMeterLimitAbove(gasLimit);
            
            StateTransitionResult stateTransitionResult = StateTransitionResult.Ok((Gas) 1000, uint160.One, new object());
            
            fixture.StateProcessor
                .Setup(sp => sp.Apply(It.IsAny<IState>(), It.IsAny<InternalCreateMessage>()))
                .Returns(stateTransitionResult);

            var internalExecutor = new InternalExecutor(
                fixture.LoggerFactory,
                fixture.Network,
                fixture.State.Object,
                fixture.StateProcessor.Object);

            ICreateResult result = internalExecutor.Create<string>(fixture.SmartContractState, amount, parameters, gasLimit);

            fixture.State.Verify(s => s.Snapshot(), Times.Once);
            
            fixture.StateProcessor.Verify(sp => 
                sp.Apply(fixture.Snapshot, It.Is<InternalCreateMessage>(m =>
                    m.Amount == amount &&
                    m.Parameters == parameters &&
                    m.GasLimit == gasLimit &&
                    m.From == fixture.FromAddress &&
                    m.Type == typeof(string).Name
                )));

            fixture.State.Verify(s => s.TransitionTo(fixture.Snapshot));

            fixture.GasMeter.Verify(g => g.Spend(stateTransitionResult.GasConsumed));

            Assert.True(result.Success);
            Assert.Equal(stateTransitionResult.Success.ContractAddress.ToAddress(fixture.Network), result.NewContractAddress);
        }

        [Fact]
        public void Create_StateTransition_Error()
        {
            ulong amount = 100UL;
            var parameters = new object[] { };
            var gasLimit = (Gas)100_000;

            var fixture = new InternalExecutorTestFixture();

            fixture.SetGasMeterLimitAbove(gasLimit);

            StateTransitionResult stateTransitionResult = StateTransitionResult.Fail((Gas)1000, new ContractErrorMessage("Error"));

            fixture.StateProcessor
                .Setup(sp => sp.Apply(It.IsAny<IState>(), It.IsAny<InternalCreateMessage>()))
                .Returns(stateTransitionResult);

            var internalExecutor = new InternalExecutor(
                fixture.LoggerFactory,
                fixture.Network,
                fixture.State.Object,
                fixture.StateProcessor.Object);

            ICreateResult result = internalExecutor.Create<string>(fixture.SmartContractState, amount, parameters, gasLimit);

            fixture.State.Verify(s => s.Snapshot(), Times.Once);

            fixture.StateProcessor.Verify(sp =>
                sp.Apply(fixture.Snapshot, It.Is<InternalCreateMessage>(m =>
                    m.Amount == amount &&
                    m.Parameters == parameters &&
                    m.GasLimit == gasLimit &&
                    m.From == fixture.FromAddress &&
                    m.Type == typeof(string).Name
                )));

            fixture.State.Verify(s => s.TransitionTo(fixture.Snapshot), Times.Never);

            fixture.GasMeter.Verify(g => g.Spend(stateTransitionResult.GasConsumed));

            Assert.False(result.Success);
            Assert.Equal(default(Address), result.NewContractAddress);
        }

        [Fact]
        public void Create_GasRemaining_Error()
        {
            ulong amount = 100UL;
            var parameters = new object[] { };
            var gasLimit = (Gas)100_000;

            var fixture = new InternalExecutorTestFixture();

            fixture.SetGasMeterLimitBelow(gasLimit);

            var internalExecutor = new InternalExecutor(
                fixture.LoggerFactory,
                fixture.Network,
                fixture.State.Object,
                fixture.StateProcessor.Object);

            ICreateResult result = internalExecutor.Create<string>(fixture.SmartContractState, amount, parameters, gasLimit);

            fixture.State.Verify(s => s.Snapshot(), Times.Never);

            fixture.StateProcessor.Verify(sp =>
                sp.Apply(fixture.Snapshot, It.IsAny<InternalCreateMessage>()), Times.Never);

            fixture.State.Verify(s => s.TransitionTo(fixture.Snapshot), Times.Never);

            fixture.GasMeter.Verify(g => g.Spend(It.IsAny<Gas>()), Times.Never);

            Assert.False(result.Success);
            Assert.Equal(default(Address), result.NewContractAddress);
        }

        [Fact]
        public void Call_StateTransition_Success()
        {
            ulong amount = 100UL;
            var to = new Address("Sj2p6ZRHdLvywyi43HYoE4bu2TF1nvavjR");
            var method = "Test";
            var parameters = new object[] { };
            var gasLimit = (Gas)100_000;

            var fixture = new InternalExecutorTestFixture();

            fixture.SetGasMeterLimitAbove(gasLimit);

            StateTransitionResult stateTransitionResult = StateTransitionResult.Ok((Gas)1000, uint160.One, new object());

            fixture.StateProcessor
                .Setup(sp => sp.Apply(It.IsAny<IState>(), It.IsAny<InternalCallMessage>()))
                .Returns(stateTransitionResult);

            var internalExecutor = new InternalExecutor(
                fixture.LoggerFactory,
                fixture.Network,
                fixture.State.Object,
                fixture.StateProcessor.Object);

            ITransferResult result = internalExecutor.Call(fixture.SmartContractState, to, amount, method, parameters, gasLimit);

            fixture.State.Verify(s => s.Snapshot(), Times.Once);

            fixture.StateProcessor.Verify(sp =>
                sp.Apply(fixture.Snapshot, It.Is<InternalCallMessage>(m =>
                    m.Amount == amount &&
                    m.Method.Name == method &&
                    m.Method.Parameters == parameters &&
                    m.GasLimit == gasLimit &&
                    m.From == fixture.FromAddress &&
                    m.To == to.ToUint160(fixture.Network)
                )));

            fixture.State.Verify(s => s.TransitionTo(fixture.Snapshot));

            fixture.GasMeter.Verify(g => g.Spend(stateTransitionResult.GasConsumed));

            Assert.True(result.Success);
            Assert.Equal(stateTransitionResult.Success.ExecutionResult, result.ReturnValue);
        }

        [Fact]
        public void Call_StateTransition_Error()
        {
            ulong amount = 100UL;
            var to = new Address("Sj2p6ZRHdLvywyi43HYoE4bu2TF1nvavjR");
            var method = "Test";
            var parameters = new object[] { };
            var gasLimit = (Gas)100_000;

            var fixture = new InternalExecutorTestFixture();

            fixture.SetGasMeterLimitAbove(gasLimit);

            StateTransitionResult stateTransitionResult = StateTransitionResult.Fail((Gas)1000, new ContractErrorMessage("Error"));

            fixture.StateProcessor
                .Setup(sp => sp.Apply(It.IsAny<IState>(), It.IsAny<InternalCallMessage>()))
                .Returns(stateTransitionResult);

            var internalExecutor = new InternalExecutor(
                fixture.LoggerFactory,
                fixture.Network,
                fixture.State.Object,
                fixture.StateProcessor.Object);

            ITransferResult result = internalExecutor.Call(fixture.SmartContractState, to, amount, method, parameters, gasLimit);

            fixture.State.Verify(s => s.Snapshot(), Times.Once);

            fixture.StateProcessor.Verify(sp =>
                sp.Apply(fixture.Snapshot, It.Is<InternalCallMessage>(m =>
                    m.Amount == amount &&
                    m.Method.Name == method &&
                    m.Method.Parameters == parameters &&
                    m.GasLimit == gasLimit &&
                    m.From == fixture.FromAddress &&
                    m.To == to.ToUint160(fixture.Network)
                )));

            fixture.State.Verify(s => s.TransitionTo(fixture.Snapshot), Times.Never);

            fixture.GasMeter.Verify(g => g.Spend(stateTransitionResult.GasConsumed), Times.Once);

            Assert.False(result.Success);
            Assert.Null(result.ReturnValue);
        }

        [Fact]
        public void Call_GasRemaining_Error()
        {
            ulong amount = 100UL;
            var to = new Address("Sj2p6ZRHdLvywyi43HYoE4bu2TF1nvavjR");
            var method = "Test";
            var parameters = new object[] { };
            var gasLimit = (Gas)100_000;

            var fixture = new InternalExecutorTestFixture();

            fixture.SetGasMeterLimitBelow(gasLimit);

            var internalExecutor = new InternalExecutor(
                fixture.LoggerFactory,
                fixture.Network,
                fixture.State.Object,
                fixture.StateProcessor.Object);

            ITransferResult result = internalExecutor.Call(fixture.SmartContractState, to, amount, method, parameters, gasLimit);

            fixture.State.Verify(s => s.Snapshot(), Times.Never);

            fixture.StateProcessor.Verify(sp =>
                sp.Apply(fixture.Snapshot, It.IsAny<InternalCreateMessage>()), Times.Never);

            fixture.State.Verify(s => s.TransitionTo(fixture.Snapshot), Times.Never);

            fixture.GasMeter.Verify(g => g.Spend(It.IsAny<Gas>()), Times.Never);

            Assert.False(result.Success);
            Assert.Null(result.ReturnValue);
        }

        [Fact]
        public void Transfer_StateTransition_Success()
        {
            ulong amount = 100UL;
            var to = new Address("Sj2p6ZRHdLvywyi43HYoE4bu2TF1nvavjR");

            var fixture = new InternalExecutorTestFixture();

            fixture.SetGasMeterLimitAbove((Gas) InternalExecutor.DefaultGasLimit);

            StateTransitionResult stateTransitionResult = StateTransitionResult.Ok((Gas)1000, uint160.One, new object());

            fixture.StateProcessor
                .Setup(sp => sp.Apply(It.IsAny<IState>(), It.IsAny<ContractTransferMessage>()))
                .Returns(stateTransitionResult);

            var internalExecutor = new InternalExecutor(
                fixture.LoggerFactory,
                fixture.Network,
                fixture.State.Object,
                fixture.StateProcessor.Object);

            ITransferResult result = internalExecutor.Transfer(fixture.SmartContractState, to, amount);

            fixture.State.Verify(s => s.Snapshot(), Times.Once);

            fixture.StateProcessor.Verify(sp =>
                sp.Apply(fixture.Snapshot, It.Is<ContractTransferMessage>(m =>
                    m.Amount == amount &&
                    m.GasLimit == InternalExecutor.DefaultGasLimit &&
                    m.From == fixture.FromAddress &&
                    m.To == to.ToUint160(fixture.Network)
                )));

            fixture.State.Verify(s => s.TransitionTo(fixture.Snapshot));

            fixture.GasMeter.Verify(g => g.Spend(stateTransitionResult.GasConsumed));

            Assert.True(result.Success);
            Assert.Null(result.ReturnValue);
        }

        [Fact]
        public void Transfer_StateTransition_Error()
        {
            ulong amount = 100UL;
            var to = new Address("Sj2p6ZRHdLvywyi43HYoE4bu2TF1nvavjR");

            var fixture = new InternalExecutorTestFixture();

            fixture.SetGasMeterLimitAbove((Gas)InternalExecutor.DefaultGasLimit);

            StateTransitionResult stateTransitionResult = StateTransitionResult.Fail((Gas)1000, new ContractErrorMessage("Error"));

            fixture.StateProcessor
                .Setup(sp => sp.Apply(It.IsAny<IState>(), It.IsAny<ContractTransferMessage>()))
                .Returns(stateTransitionResult);

            var internalExecutor = new InternalExecutor(
                fixture.LoggerFactory,
                fixture.Network,
                fixture.State.Object,
                fixture.StateProcessor.Object);

            ITransferResult result = internalExecutor.Transfer(fixture.SmartContractState, to, amount);

            fixture.State.Verify(s => s.Snapshot(), Times.Once);

            fixture.StateProcessor.Verify(sp =>
                sp.Apply(fixture.Snapshot, It.Is<ContractTransferMessage>(m =>
                    m.Amount == amount &&
                    m.GasLimit == InternalExecutor.DefaultGasLimit &&
                    m.From == fixture.FromAddress &&
                    m.To == to.ToUint160(fixture.Network)
                )));

            fixture.State.Verify(s => s.TransitionTo(fixture.Snapshot), Times.Never);

            fixture.GasMeter.Verify(g => g.Spend(stateTransitionResult.GasConsumed), Times.Once);

            Assert.False(result.Success);
            Assert.Null(result.ReturnValue);
        }

        [Fact]
        public void Transfer_GasRemaining_Error()
        {
            ulong amount = 100UL;
            var to = new Address("Sj2p6ZRHdLvywyi43HYoE4bu2TF1nvavjR");

            var fixture = new InternalExecutorTestFixture();

            fixture.SetGasMeterLimitBelow((Gas) InternalExecutor.DefaultGasLimit);

            var internalExecutor = new InternalExecutor(
                fixture.LoggerFactory,
                fixture.Network,
                fixture.State.Object,
                fixture.StateProcessor.Object);

            ITransferResult result = internalExecutor.Transfer(fixture.SmartContractState, to, amount);

            fixture.State.Verify(s => s.Snapshot(), Times.Never);

            fixture.StateProcessor.Verify(sp =>
                sp.Apply(fixture.Snapshot, It.IsAny<ContractTransferMessage>()), Times.Never);

            fixture.State.Verify(s => s.TransitionTo(fixture.Snapshot), Times.Never);

            fixture.GasMeter.Verify(g => g.Spend(It.IsAny<Gas>()), Times.Never);

            Assert.False(result.Success);
            Assert.Null(result.ReturnValue);
        }
    }
}