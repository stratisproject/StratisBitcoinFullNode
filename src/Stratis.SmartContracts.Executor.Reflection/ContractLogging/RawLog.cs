using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection.ContractLogging
{
    public class RawLog
    {
        public uint160 ContractAddress { get; }

        public object LogStruct { get; }

        public RawLog(uint160 contractAddress, object log)
        {
            this.ContractAddress = contractAddress;
            this.LogStruct = log;
        }

        public Log ToLog(IContractPrimitiveSerializer serializer)
        {
            List<byte[]> topics = new List<byte[]>();

            // first topic is the log type name
            topics.Add(Encoding.UTF8.GetBytes(this.LogStruct.GetType().Name));

            // rest of the topics are the indexed fields. TODO: This currently gets all fields.
            foreach (FieldInfo field in this.LogStruct.GetType().GetFields())
            {
                object value = field.GetValue(this.LogStruct);
                byte[] serialized = serializer.Serialize(value);
                topics.Add(serialized);
            }

            return new Log(this.ContractAddress, topics, serializer.Serialize(this.LogStruct));
        }
    }

    public static class RawLogExtensions
    {
        public static IList<Log> ToLogs(this IList<RawLog> rawLogs, IContractPrimitiveSerializer contractPrimitiveSerializer)
        {
            return rawLogs.Select(x=>x.ToLog(contractPrimitiveSerializer)).ToList();
        }
    }
}
