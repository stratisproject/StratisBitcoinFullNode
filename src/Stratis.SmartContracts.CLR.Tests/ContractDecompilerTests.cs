using System.IO;
using CSharpFunctionalExtensions;
using Stratis.SmartContracts.CLR.Compilation;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ContractDecompilerTests
    {
        /// <summary>
        /// https://github.com/jbevain/cecil/issues/556
        /// </summary>
        [Fact]
        public void MonoCecil_Doesnt_Throw_Unexpected_Exceptions()
        {
            string[] files = Directory.GetFiles("Modules");

            foreach (string file in files)
            {
                this.TestFileDecompilesCleanly(file);
            }

            // No exceptions thrown!
        }

        private void TestFileDecompilesCleanly(string filename)
        {
            byte[] bytes = File.ReadAllBytes(filename);
            Result<IContractModuleDefinition> result = ContractDecompiler.GetModuleDefinition(bytes);
        }
    }
}
