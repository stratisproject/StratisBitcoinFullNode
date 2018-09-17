﻿using NBitcoin;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractStateFactory
    {
        /// <summary>
        /// Sets up a new <see cref="ISmartContractState"/> based on the current state.
        /// </summary>        
        ISmartContractState Create(IState state, IGasMeter gasMeter, uint160 address, BaseMessage message,
            IContractState repository);
    }
}