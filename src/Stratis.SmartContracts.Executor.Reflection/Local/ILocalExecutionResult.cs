﻿using System.Collections.Generic;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Executor.Reflection.Local
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