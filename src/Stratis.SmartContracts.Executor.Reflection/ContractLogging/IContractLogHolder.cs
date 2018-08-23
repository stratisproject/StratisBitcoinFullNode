using System.Collections.Generic;

namespace Stratis.SmartContracts.Executor.Reflection.ContractLogging
{
    public interface IContractLogHolder : IContractLogger
    {
        IList<RawLog> GetRawLogs();

        void AddRawLogs(IEnumerable<RawLog> toAdd);
    }
}
