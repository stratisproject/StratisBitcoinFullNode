﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Stratis.SmartContracts.Executor.Reflection.Compilation
{
    /// <summary>
    /// The result of a Smart Contract compilation.
    /// If compilation was successful, the <see cref="Compilation"/> 
    /// property will contain the contract's compiled bytecode and <see cref="Success"/> will be true.
    /// If unsuccesssful, the emitted diagnostics will be present in <see cref="Diagnostics"/> 
    /// and <see cref="Success"/> will be false.
    /// </summary>
    public class SmartContractCompilationResult
    {
        private SmartContractCompilationResult(byte[] compilation)
        {
            this.Success = true;
            this.Compilation = compilation;
            this.Diagnostics = Enumerable.Empty<Diagnostic>();
        }

        private SmartContractCompilationResult(IEnumerable<Diagnostic> emitResultDiagnostics)
        {
            this.Success = false;
            this.Diagnostics = emitResultDiagnostics ?? Enumerable.Empty<Diagnostic>();
        }

        public IEnumerable<Diagnostic> Diagnostics { get; }

        public byte[] Compilation { get; }
        
        public bool Success { get; }

        public static SmartContractCompilationResult Succeeded(byte[] toArray)
        {
            return new SmartContractCompilationResult(toArray);
        }

        public static SmartContractCompilationResult Failed(IEnumerable<Diagnostic> emitResultDiagnostics)
        {
            return new SmartContractCompilationResult(emitResultDiagnostics);
        }
    }
}