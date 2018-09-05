using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Executor.Reflection.ContractLogging
{
    public class ContractLogHolder : IContractLogHolder
    {
        private readonly Network network;
        private readonly List<RawLog> rawLogs;

        public ContractLogHolder(Network network)
        {
            this.network = network;
            this.rawLogs = new List<RawLog>();
        }

        /// <inheritdoc />
        public void Log<T>(ISmartContractState smartContractState, T toLog)
        {
            this.rawLogs.Add(new RawLog(smartContractState.Message.ContractAddress.ToUint160(this.network), toLog));
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
