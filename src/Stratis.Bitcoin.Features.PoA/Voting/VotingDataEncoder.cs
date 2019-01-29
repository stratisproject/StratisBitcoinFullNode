using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class VotingDataEncoder
    {
        /// <summary>Prefix used to identify OP_RETURN output with voting data.</summary>
        public static readonly byte[] VotingOutputPrefixBytes = new byte[] { 143, 18, 13, 254 };

        public const int VotingDataMaxSerializedSize = ushort.MaxValue;

        private readonly ILogger logger;

        public VotingDataEncoder(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>Decodes raw voting data.</summary>
        /// <exception cref="PoAConsensusErrors.VotingDataInvalidFormat">Thrown in case voting data format is invalid.</exception>
        public List<VotingData> Decode(byte[] votingDataBytes)
        {
            try
            {
                if (votingDataBytes.Length > VotingDataMaxSerializedSize)
                {
                    this.logger.LogTrace("(-)[INVALID_SIZE]");
                    PoAConsensusErrors.VotingDataInvalidFormat.Throw();
                }

                using (var memoryStream = new MemoryStream(votingDataBytes))
                {
                    var deserializeStream = new BitcoinStream(memoryStream, false);

                    var decoded = new List<VotingData>();

                    deserializeStream.ReadWrite(ref decoded);

                    return decoded;
                }
            }
            catch (Exception e)
            {
                this.logger.LogDebug("Exception during deserialization: '{0}'.", e.ToString());
                this.logger.LogTrace("(-)[DESERIALIZING_EXCEPTION]");

                PoAConsensusErrors.VotingDataInvalidFormat.Throw();
                return null;
            }
        }

        /// <summary>Encodes voting data collection.</summary>
        public byte[] Encode(List<VotingData> votingData)
        {
            using (var memoryStream = new MemoryStream())
            {
                var serializeStream = new BitcoinStream(memoryStream, true);

                serializeStream.ReadWrite(ref votingData);

                return memoryStream.ToArray();
            }
        }

        /// <summary>Provides voting output data from transaction's coinbase voting output.</summary>
        /// <exception cref="PoAConsensusErrors.TooManyVotingOutputs">Thrown in case more than one voting output is found.</exception>
        /// <returns>Voting script or <c>null</c> if voting script wasn't found.</returns>
        public byte[] ExtractRawVotingData(Transaction tx)
        {
            IEnumerable<Script> opReturnOutputs = tx.Outputs.Where(x => (x.ScriptPubKey.Length > 0) && (x.ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_RETURN))
                .Select(x => x.ScriptPubKey);

            byte[] votingData = null;

            foreach (Script script in opReturnOutputs)
            {
                IEnumerable<Op> ops = script.ToOps();

                if (ops.Count() != 2)
                    continue;

                byte[] data = ops.Last().PushData;

                bool correctPrefix = data.Take(VotingOutputPrefixBytes.Length).SequenceEqual(VotingOutputPrefixBytes);

                if (!correctPrefix)
                    continue;

                if (votingData != null)
                {
                    this.logger.LogTrace("(-)[TOO_MANY_VOTING_OUTPUTS]");
                    PoAConsensusErrors.TooManyVotingOutputs.Throw();
                }

                votingData = data.Skip(VotingOutputPrefixBytes.Length).ToArray();
            }

            return votingData;
        }
    }
}
