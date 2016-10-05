using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
    public class ConsensusErrorException : Exception
    {
        public ConsensusErrorException(ConsensusError error) : base(error.Message)
        {

        }
    }
    public class ConsensusError
    {
        private readonly string _Code;
        public string Code
        {
            get
            {
                return _Code;
            }
        }

        private readonly string _Message;
        public string Message
        {
            get
            {
                return _Message;
            }
        }

        public void Throw()
        {
            throw new ConsensusErrorException(this);
        }

        public ConsensusError(string code, string message)
        {
            if(code == null)
                throw new ArgumentNullException("code");
            if(message == null)
                throw new ArgumentNullException("message");
            _Code = code;
            _Message = message;
        }

        public override string ToString()
        {
            return _Code + ": " + _Message;
        }
    }
    public class ConsensusErrors
    {
        public readonly static ConsensusError HighHash = new ConsensusError("high-hash", "proof of work failed");
        public readonly static ConsensusError BadCoinbaseHeight = new ConsensusError("bad-cb-height", "block height mismatch in coinbase");
        public readonly static ConsensusError BadTransactionNonFinal = new ConsensusError("bad-txns-nonfinal", "non-final transaction");
        public readonly static ConsensusError BadWitnessNonceSize = new ConsensusError("bad-witness-nonce-size", "invalid witness nonce size");
        public readonly static ConsensusError BadWitnessMerkleMatch = new ConsensusError("bad-witness-merkle-match", "witness merkle commitment mismatch");
        public readonly static ConsensusError UnexpectedWitness = new ConsensusError("unexpected-witness", "unexpected witness data found");
        public readonly static ConsensusError BadBlockWeight = new ConsensusError("bad-blk-weight", "weight limit failed");
        public readonly static ConsensusError BadDiffBits = new ConsensusError("bad-diffbits", "incorrect proof of work");
        public readonly static ConsensusError TimeTooOld = new ConsensusError("time-too-old", "block's timestamp is too early");
        public readonly static ConsensusError TimeTooNew = new ConsensusError("time-too-new", "block timestamp too far in the future");
        public readonly static ConsensusError BadVersion = new ConsensusError("bad-version", "block version rejected");
    }
}
