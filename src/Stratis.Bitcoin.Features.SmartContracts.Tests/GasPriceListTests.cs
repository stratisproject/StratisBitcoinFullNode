using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core.Backend;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class GasPriceListTests
    {
        [Fact]
        public void SmartContracts_GasPrice_TestNoOperandInstructionPrices()
        {
            var priceList = new Dictionary<Instruction, ulong>
            {
                { Instruction.Create(OpCodes.Nop), 1  }
            };

            foreach (var kvp in priceList)
            {
                Instruction instruction = kvp.Key;
                ulong price = kvp.Value;

                Assert.Equal(price, GasPriceList.InstructionOperationCost(instruction));
            }
        }

        [Fact]
        public void SmartContracts_GasPrice_TestOperandInstructionPrices()
        {
            var priceList = new Dictionary<Instruction, ulong>
            {
                { Instruction.Create(OpCodes.Ldstr, "test"), 1  }
            };

            foreach (var kvp in priceList)
            {
                Instruction instruction = kvp.Key;
                ulong price = kvp.Value;

                Assert.Equal(price, GasPriceList.InstructionOperationCost(instruction));
            }
        }

        [Fact]
        public void SmartContracts_GasPrice_TestStorageOperationPrices()
        {
            byte[] key = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] value= new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            Gas cost = (Gas) (20000 * key.Length + 20000 * value.Length);

            Assert.Equal(cost, GasPriceList.StorageOperationCost(key, value));
        }

        [Fact]
        public void SmartContracts_GasPrice_TestMethodCallPrices()
        {
            var module = typeof(object).Module.FullyQualifiedName;

            var moduleDefinition = ModuleDefinition.ReadModule(module);

            var type = moduleDefinition.Types.First(t => t.FullName.Contains("DateTime"));
            var method = type.Methods.First(m => m.FullName.Contains("Parse"));

            Assert.Equal((Gas)0, GasPriceList.MethodCallCost(method));
        }
    }
}