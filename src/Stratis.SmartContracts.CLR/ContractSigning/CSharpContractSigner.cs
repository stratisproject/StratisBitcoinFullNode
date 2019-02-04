using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Core.ContractSigning;

namespace Stratis.SmartContracts.CLR.ContractSigning
{
    public class CSharpContractSigner
    {
        private readonly IContractSigner contractSigner;

        public CSharpContractSigner(IContractSigner contractSigner)
        {
            this.contractSigner = contractSigner;
        }

        /// <summary>
        /// Compile and then sign a .cs file to enable use as a smart contract.
        /// </summary>
        public (byte[] contractCode, byte[] signature) SignCSharpFile(Key privKey, string path)
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile(path);

            if (!compilationResult.Success)
                throw new Exception("Contract compilation failed.");

            byte[] signature = this.contractSigner.Sign(privKey, compilationResult.Compilation);

            return (compilationResult.Compilation, signature);
        } 
    }
}
