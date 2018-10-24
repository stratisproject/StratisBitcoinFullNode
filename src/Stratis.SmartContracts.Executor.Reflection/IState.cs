﻿using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IState
    {
        IBlock Block { get; }
        BalanceState BalanceState { get; }
        IStateRepository ContractState { get; }
        IList<Log> GetLogs(IContractPrimitiveSerializer serializer);
        IReadOnlyList<TransferInfo> InternalTransfers { get; }
        IContractLogHolder LogHolder { get; }
        IState Snapshot();
        NonceGenerator NonceGenerator { get; }
        void TransitionTo(IState state);
        void AddInternalTransfer(TransferInfo transferInfo);
        ulong GetBalance(uint160 address);
        uint160 GenerateAddress(IAddressGenerator addressGenerator);
        ISmartContractState CreateSmartContractState(IState state, IGasMeter gasMeter, uint160 address, BaseMessage message, IStateRepository repository);
        void AddInitialTransfer(TransferInfo initialTransfer);
    }
}