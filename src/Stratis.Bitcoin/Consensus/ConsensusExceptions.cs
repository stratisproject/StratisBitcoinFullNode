using System;

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

    public class InvalidHeaderException : ConsensusException
    {
        public InvalidHeaderException() : base()
        {
        }
    }
}