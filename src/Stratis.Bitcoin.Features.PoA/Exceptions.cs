using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.PoA
{
    public class NotAFederationMemberException : Exception
    {
        public NotAFederationMemberException() : base("Not a federation member!")
        {
        }
    }
}
