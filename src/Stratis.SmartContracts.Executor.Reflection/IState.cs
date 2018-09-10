using System.Collections.Generic;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IState
    {
        IList<Log> GetLogs();
        IReadOnlyList<TransferInfo> InternalTransfers { get; }
        IContractLogHolder LogHolder { get; }
        StateTransitionResult Apply(ExternalCreateMessage message);
        StateTransitionResult Apply(InternalCreateMessage message);
        StateTransitionResult Apply(ExternalCallMessage message);
        StateTransitionResult Apply(InternalCallMessage message);
        StateTransitionResult Apply(ContractTransferMessage message);
    }
}