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

    public class HeaderInvalidException : ConsensusException
    {
        public HeaderInvalidException() : base()
        {
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