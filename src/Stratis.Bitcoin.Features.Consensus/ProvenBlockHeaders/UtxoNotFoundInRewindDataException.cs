using System;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class UtxoNotFoundInRewindDataException : Exception
    {
        public UtxoNotFoundInRewindDataException(string message) : base(message)
        {
        }
    }
}
