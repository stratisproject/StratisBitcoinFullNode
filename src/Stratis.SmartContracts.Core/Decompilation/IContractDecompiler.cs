using System;
using System.Collections.Generic;
using System.Text;
using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.Core.Decompilation
{
    /// <summary>
    /// Decompiles a contract from bytecode to source code.
    /// </summary>
    public interface IContractDecompiler
    {
        Result<string> GetSource(byte[] bytecode);
    }
}
