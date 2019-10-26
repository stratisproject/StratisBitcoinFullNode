using System.Collections.Generic;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.CLR.Local
{
    public class LocalExecutionResult : ILocalExecutionResult
    {
        public IReadOnlyList<TransferInfo> InternalTransfers { get; set; }
        public RuntimeObserver.Gas GasConsumed { get; set; }
        public bool Revert { get; set; }
        public ContractErrorMessage ErrorMessage { get; set; }
        public object Return { get; set; }
        public IList<Log> Logs { get; set; }
    }
}