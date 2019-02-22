using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.CLR.Serialization;

namespace Stratis.SmartContracts.CLR.ContractLogging
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
        /// 
        /// TODO: Cache this value.
        /// </summary>
        public Log ToLog(IContractPrimitiveSerializer serializer)
        {
            (List<byte[]> topics, List<byte[]> fields) = this.Serialize(serializer);

            byte[] encodedFields = RLP.EncodeList(fields.Select(RLP.EncodeElement).ToArray());

            return new Log(this.ContractAddress, topics, encodedFields);
        }

        /// <summary>
        /// Serializes the log and topics to bytes.
        /// </summary>
        /// <returns></returns>
        private (List<byte[]>, List<byte[]>) Serialize(IContractPrimitiveSerializer serializer)
        {
            Type logType = this.LogStruct.GetType();

            // first topic is the log type name
            byte[] logTypeName = serializer.Serialize(logType.Name);
            
            var topics = new List<byte[]> { logTypeName };

            var fields = new List<byte[]>();

            // rest of the topics are the indexed fields.
            foreach (FieldInfo field in logType.GetFields())
            {
                object value = field.GetValue(this.LogStruct);

                byte[] serialized = value != null 
                    ? serializer.Serialize(value)
                    : new byte[0];

                if (field.CustomAttributes.Any(y => y.AttributeType == typeof(IndexAttribute)))
                {
                    // It's an index, add to the topics
                    topics.Add(serialized);
                }

                fields.Add(serialized);
            }

            return (topics, fields);
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
