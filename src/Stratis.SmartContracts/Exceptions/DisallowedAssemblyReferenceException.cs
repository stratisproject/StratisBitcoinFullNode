using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Exceptions
{
    public class DisallowedAssemblyReferenceException : StratisCompilationException
    {
        public DisallowedAssemblyReferenceException() { }
        public DisallowedAssemblyReferenceException(string message) : base(message) { }
    }
}
