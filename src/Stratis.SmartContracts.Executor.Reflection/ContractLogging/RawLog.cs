using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection.ContractLogging
{
    /// <summary>
    /// Holds logs straight out of a smart contract interaction, before
    /// they are ready to be used in consensus.
    /// </summary>
    public class RawLog
    {
        /// <summary>
        /// Contract inside which this was logged.
        /// </summary>
        public uint160 ContractAddress { get; }

        /// <summary>
        /// The actual struct logged inside the contract.
        /// </summary>
        public object LogStruct { get; }

        public RawLog(uint160 contractAddress, object log)
        {
            this.ContractAddress = contractAddress;
            this.LogStruct = log;
        }

        /// <summary>
        /// Transforms this log into the log type used by consensus.
        /// </summary>
        public Log ToLog(IContractPrimitiveSerializer serializer)
        {
            var topics = new List<byte[]>();

            // first topic is the log type name
            topics.Add(Encoding.UTF8.GetBytes(this.LogStruct.GetType().Name));

            // rest of the topics are the indexed fields.
            foreach (FieldInfo field in this.LogStruct.GetType().GetFields().Where(x=>x.CustomAttributes.Any(y=>y.AttributeType == typeof(IndexAttribute))))
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
        /// <summary>
        /// Transforms all of the logs into the log type used by consensus.
        /// </summary>
        public static IList<Log> ToLogs(this IList<RawLog> rawLogs, IContractPrimitiveSerializer contractPrimitiveSerializer)
        {
            return rawLogs.Select(x=>x.ToLog(contractPrimitiveSerializer)).ToList();
        }
    }
}
