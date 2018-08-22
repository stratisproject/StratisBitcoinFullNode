using System;
using System.Collections.Generic;
using System.Reflection;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;

namespace Stratis.SmartContracts.Executor.Reflection.ContractLogging
{
    public class ContractLogHolder : IContractLogger
    {
        private readonly Network network;
        private readonly List<LogHolder> logs;

        public ContractLogHolder(Network network)
        {
            this.logs = new List<LogHolder>();
            this.network = network;
        }

        public void Log<T>(ISmartContractState smartContractState, T toLog)
        {
            this.logs.Add(new LogHolder(smartContractState.Message.ContractAddress.ToUint160(this.network), toLog));
        }

        public IList<Log> GetLogs()
        {
            List<Log> actualLogs = new List<Log>();
            foreach(LogHolder log in this.logs)
            {
                foreach (FieldInfo field in log.LogObject.GetType().GetFields())
                {
                    object value = field.GetValue(log.LogObject);
                    byte[] serialized = Serialize(value, network);
                    toEncode.Add(RLP.EncodeElement(serialized));
                }
            }

            throw new NotImplementedException();
        }
    }

    public class LogHolder
    {
        public uint160 ContractAddress { get; }

        public object LogObject { get; }

        public LogHolder(uint160 contractAddress, object log)
        {
            this.ContractAddress = contractAddress;
            this.LogObject = log;
        }
    }
}
