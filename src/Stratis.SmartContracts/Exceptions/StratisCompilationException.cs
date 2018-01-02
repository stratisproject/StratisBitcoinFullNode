using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.SmartContracts.Exceptions
{
    public class StratisCompilationException : Exception
    {
        public StratisCompilationException() { }
        public StratisCompilationException(string message): base(message) { }
    }
}
