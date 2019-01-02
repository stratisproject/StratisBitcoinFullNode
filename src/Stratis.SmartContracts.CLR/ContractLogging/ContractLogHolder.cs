using System.Collections.Generic;

namespace Stratis.SmartContracts.CLR.ContractLogging
{
    public class ContractLogHolder : IContractLogHolder
    {
        private readonly List<RawLog> rawLogs;

        public ContractLogHolder()
        {
            this.rawLogs = new List<RawLog>();
        }

        /// <inheritdoc />
        public void Log<T>(ISmartContractState smartContractState, T toLog) where T : struct 
        {
            this.rawLogs.Add(new RawLog(smartContractState.Message.ContractAddress.ToUint160(), toLog));
        }

        /// <inheritdoc />
        public IList<RawLog> GetRawLogs()
        {
            return this.rawLogs;
        }

        /// <inheritdoc />
        public void AddRawLogs(IEnumerable<RawLog> toAdd)
        {
            this.rawLogs.AddRange(toAdd);
        }

        /// <inheritdoc />
        public void Clear()
        {
            this.rawLogs.Clear();
        }
    }
}
