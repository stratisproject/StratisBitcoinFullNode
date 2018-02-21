using System.Text;
using Stratis.SmartContracts.Backend;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class GasCostTests
    {
        [Theory]
        [InlineData("nop", "", 1)]
        public void SmartContract_GasCost_Instruction(string opcode, string operand, ulong expectedCost)
        {
            GasSpendOperation gasSpendOperation = GasOperationCostFactory.Create(opcode, operand);

            Assert.Equal(expectedCost, gasSpendOperation.Cost);
        }

        [Theory]
        [InlineData("1", "1", 40000)]
        [InlineData("1", "11", 60000)]
        [InlineData("1", "111", 80000)]
        [InlineData("11", "1", 60000)]
        [InlineData("11", "11", 80000)]
        [InlineData("111", "11", 100000)]
        public void SmartContract_GasCost_Storage(string key, string value, ulong expectedCost)
        {
            GasSpendOperation gasSpendOperation =
                GasOperationCostFactory.CreateStorageOperation(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value));

            Assert.Equal(expectedCost, gasSpendOperation.Cost);
        }
    }
}