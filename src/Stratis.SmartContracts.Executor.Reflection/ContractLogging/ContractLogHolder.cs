using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection.ContractLogging
{
    public class ContractLogHolder : IContractLogger
    {
        private readonly IContractPrimitiveSerializer serializer;
        private readonly Network network;
        private readonly List<RawLog> rawLogs;

        public ContractLogHolder(IContractPrimitiveSerializer serializer, Network network)
        {
            this.serializer = serializer;
            this.network = network;
            this.rawLogs = new List<RawLog>();
        }

        public void Log<T>(ISmartContractState smartContractState, T toLog)
        {
            this.rawLogs.Add(new RawLog(smartContractState.Message.ContractAddress.ToUint160(this.network), toLog));
        }

        public IList<Log> GetLogs()
        {
            List<Log> logs = new List<Log>();
            foreach(RawLog rawLog in this.rawLogs)
            {
                List<byte[]> topics = new List<byte[]>();

                // first topic is the log type name
                topics.Add(Encoding.UTF8.GetBytes(rawLog.LogStruct.GetType().Name));

                // rest of the topics are the indexed fields. TODO: This currently gets all fields.
                foreach (FieldInfo field in rawLog.LogStruct.GetType().GetFields())
                {
                    object value = field.GetValue(rawLog.LogStruct);
                    byte[] serialized = this.serializer.Serialize(value);
                    topics.Add(serialized);
                }

                logs.Add(new Log(rawLog.ContractAddress, topics, this.serializer.Serialize(rawLog.LogStruct)));
            }

            return logs;
        }
    }

    public class RawLog
    {
        public uint160 ContractAddress { get; }

        public object LogStruct { get; }

        public RawLog(uint160 contractAddress, object log)
        {
            this.ContractAddress = contractAddress;
            this.LogStruct = log;
        }
    }
}
