using System;
using System.Net;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusException : Exception
    {
        protected ConsensusException() : base()
        {
        }

        public ConsensusException(string messsage) : base(messsage)
        {
        }
    }

    public class MaxReorgViolationException : ConsensusException
    {
        public MaxReorgViolationException() : base()
        {
        }
    }

    public class ConnectHeaderException : ConsensusException
    {
        public ConnectHeaderException() : base()
        {
        }
    }

    /// <summary>
    /// This throws when the header of a previously block that failed
    /// partial or full validation and was marked as invalid is passed to the node.
    /// </summary>
    public class HeaderInvalidException : ConsensusException
    {
        public HeaderInvalidException() : base()
        {
        }
    }

    /// <summary>
    /// An exception that is contains exception coming from the <see cref="IConsensusRuleEngine"/> execution.
    /// </summary>
    public class ConsensusRuleException : ConsensusException
    {
        public ConsensusError ConsensusError { get; }

        public ConsensusRuleException(ConsensusError consensusError) : base(consensusError.ToString())
        {
            this.ConsensusError = consensusError;
        }
    }

    public class CheckpointMismatchException : ConsensusException
    {
        public CheckpointMismatchException() : base()
        {
        }
    }

    public class BlockDownloadedForMissingChainedHeaderException : ConsensusException
    {
        public BlockDownloadedForMissingChainedHeaderException() : base()
        {
        }
    }

    public class IntegrityValidationFailedException : ConsensusException
    {
        /// <summary>The peer this block came from.</summary>
        public IPEndPoint PeerEndPoint { get; }

        /// <summary>Consensus error.</summary>
        public ConsensusError Error { get; }

        /// <summary>Time for which peer should be banned.</summary>
        public int BanDurationSeconds { get; }

        public IntegrityValidationFailedException(IPEndPoint peer, ConsensusError error, int banDurationSeconds)
        {
            this.PeerEndPoint = peer;
            this.Error = error;
            this.BanDurationSeconds = banDurationSeconds;
        }
    }
}