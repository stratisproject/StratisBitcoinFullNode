using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class SmartContractScriptTests
    {
        [Fact]
        public void IsSmartContractExec_CallContract_Success()
        {
            var script = new NBitcoin.Script(new byte[] {(byte) ScOpcodeType.OP_CALLCONTRACT});

            Assert.True(script.IsSmartContractExec());
        }

        [Fact]
        public void IsSmartContractExec_CallContract_Failure()
        {
            var script = new NBitcoin.Script(new byte[] { 0xFF, (byte)ScOpcodeType.OP_CALLCONTRACT, 0xFF });

            Assert.False(script.IsSmartContractExec());
        }

        [Fact]
        public void IsSmartContractExec_CreateContract_Success()
        {
            var script = new NBitcoin.Script(new byte[] { (byte)ScOpcodeType.OP_CREATECONTRACT });

            Assert.True(script.IsSmartContractExec());
        }

        [Fact]
        public void IsSmartContractExec_CreateContract_Failure()
        {
            var script = new NBitcoin.Script(new byte[] { 0xFF, (byte)ScOpcodeType.OP_CREATECONTRACT, 0xFF });

            Assert.False(script.IsSmartContractExec());
        }

        [Fact]
        public void IsSmartContractCall_Success()
        {
            var script = new NBitcoin.Script(new byte[] { (byte)ScOpcodeType.OP_CALLCONTRACT });

            Assert.True(script.IsSmartContractCall());
        }

        [Fact]
        public void IsSmartContractCall_Failure()
        {
            var script = new NBitcoin.Script(new byte[] { 0xFF, (byte)ScOpcodeType.OP_CALLCONTRACT, 0xFF });

            Assert.False(script.IsSmartContractCall());
        }

        [Fact]
        public void IsSmartContractCreate_Success()
        {
            var script = new NBitcoin.Script(new byte[] { (byte)ScOpcodeType.OP_CREATECONTRACT });

            Assert.True(script.IsSmartContractCreate());
        }

        [Fact]
        public void IsSmartContractCreate_Failure()
        {
            var script = new NBitcoin.Script(new byte[] { 0xFF, (byte)ScOpcodeType.OP_CREATECONTRACT, 0xFF });

            Assert.False(script.IsSmartContractCreate());
        }

        [Fact]
        public void IsSmartContractSpend_Success()
        {
            var script = new NBitcoin.Script(new byte[] { (byte)ScOpcodeType.OP_SPEND });

            Assert.True(script.IsSmartContractSpend());
        }

        [Fact]
        public void IsSmartContractSpend_Failure()
        {
            var script = new NBitcoin.Script(new byte[] { 0xFF, (byte)ScOpcodeType.OP_SPEND, 0xFF });

            Assert.False(script.IsSmartContractSpend());
        }

        [Fact]
        public void IsSmartContractInternalCall_Success()
        {
            var script = new NBitcoin.Script(new byte[] { (byte)ScOpcodeType.OP_INTERNALCONTRACTTRANSFER });

            Assert.True(script.IsSmartContractInternalCall());
        }

        [Fact]
        public void IsSmartContractInternalCall_Failure()
        {
            var script = new NBitcoin.Script(new byte[] { 0xFF, (byte)ScOpcodeType.OP_INTERNALCONTRACTTRANSFER, 0xFF });

            Assert.False(script.IsSmartContractInternalCall());
        }
    }
}
