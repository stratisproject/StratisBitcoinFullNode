﻿using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class InternalExecutorTestFixture
    {
        public InternalExecutorTestFixture()
        {
            // The addresses used in related tests are for this Network
            this.Network = new SmartContractPosTest();

            var logger = new Mock<ILogger>();
            this.LoggerFactory = Mock.Of<ILoggerFactory>
                (l => l.CreateLogger(It.IsAny<string>()) == logger.Object);

            this.Snapshot = Mock.Of<IState>();
            
            var state = new Mock<IState>();
            state.Setup(s => s.Snapshot()).Returns(this.Snapshot);

            this.State = state;
            this.StateProcessor = new Mock<IStateProcessor>();

            this.GasMeter = new Mock<IGasMeter>();

            var smartContractState = Mock.Of<ISmartContractState>(s =>
                s.Message == new Message((Address)"SeMvVcDKTLBrxVua5GXmdF8qBYTbJZt4NJ", (Address)"Sipqve53hyjzTo2oU7PUozpT1XcmATnkTn", 100) &&
                s.GasMeter == this.GasMeter.Object);

            this.SmartContractState = smartContractState;

            this.FromAddress = smartContractState.Message.ContractAddress.ToUint160(this.Network);
        }

        public uint160 FromAddress { get; }

        public ISmartContractState SmartContractState { get; }

        public Mock<IGasMeter> GasMeter { get; }

        public IState Snapshot { get; }

        public Mock<IStateProcessor> StateProcessor { get; }

        public Mock<IState> State { get; }

        public Network Network { get; }

        public ILoggerFactory LoggerFactory { get; }

        public void SetGasMeterLimitAbove(Gas minimum)
        {
            this.GasMeter.SetupGet(g => g.GasAvailable).Returns((Gas)(minimum + 1));
        }

        public void SetGasMeterLimitBelow(Gas maximum)
        {
            this.GasMeter.SetupGet(g => g.GasAvailable).Returns((Gas)(maximum - 1));
        }
    }
}