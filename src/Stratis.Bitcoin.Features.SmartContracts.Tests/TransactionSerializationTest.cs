using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Util;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class TransactionSerializationTest
    {
        [Fact]
        public void SerializeTest()
        {
            SmartContractTransaction contractTransaction = new SmartContractTransaction
            {
                VmVersion = 1,
                GasLimit = 500000,
                GasPrice = 1,
                ContractCode = new byte[] {0,1,2,3,4},
                OpCodeType = OpcodeType.OP_CREATECONTRACT,
                Parameters = new object[] {1, "test"}
            };

            Transaction tx = new Transaction();
            tx.AddInput(new TxIn(new OutPoint(0, 0), new Script(OpcodeType.OP_1)));
            tx.AddOutput(new TxOut(new Money(5000000000L - 10000), new Script(contractTransaction.ToBytes())));
            SmartContractTransaction deserializedTransaction = new SmartContractTransaction(tx.Outputs[0], tx);
            Assert.Equal(contractTransaction.VmVersion, deserializedTransaction.VmVersion);
            Assert.Equal(contractTransaction.GasLimit, deserializedTransaction.GasLimit);
            Assert.Equal(contractTransaction.GasPrice, deserializedTransaction.GasPrice);
            Assert.Equal(contractTransaction.ContractCode, deserializedTransaction.ContractCode);
            Assert.Equal(contractTransaction.OpCodeType, deserializedTransaction.OpCodeType);
            // here is where it starts breaking
            Assert.Equal(contractTransaction.Parameters[0], deserializedTransaction.Parameters[0]);
        }
    }
}
