using System.Collections.Generic;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.Local
{
    public interface ILocalExecutionResult
    {
        IReadOnlyList<TransferInfo> InternalTransfers { get; }
        Gas GasConsumed { get; }
        bool Revert { get; }
        ContractErrorMessage ErrorMessage { get; }
        object Return { get; }
        IList<Log> Logs { get; }
    }
}