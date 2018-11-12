using System;

namespace Stratis.Bitcoin.Features.ProvenHeaders
{
    public class ProvenBlockHeaderException : Exception
    {
        public ProvenBlockHeaderException(string message) : base(message)
        {
        }
    }
}
