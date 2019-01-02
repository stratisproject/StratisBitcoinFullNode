using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public static class SmartContractScript
    {
        public static bool IsSmartContractExec(this Script script)
        {
            return script.IsSmartContractCall() || script.IsSmartContractCreate();
        }

        public static bool IsSmartContractCall(this Script script)
        {
            return TestFirstByte(script, (byte)ScOpcodeType.OP_CALLCONTRACT);
        }

        public static bool IsSmartContractCreate(this Script script)
        {
            return TestFirstByte(script, (byte)ScOpcodeType.OP_CREATECONTRACT);
        }

        public static bool IsSmartContractSpend(this Script script)
        {
            return TestFirstByte(script, (byte)ScOpcodeType.OP_SPEND);

        }

        public static bool IsSmartContractInternalCall(this Script script)
        {
            return TestFirstByte(script, (byte) ScOpcodeType.OP_INTERNALCONTRACTTRANSFER);
        }

        private static bool TestFirstByte(Script script, byte opcode)
        {
            var scriptBytes = script.ToBytes();

            if (scriptBytes.Length == 0)
                return false;

            return scriptBytes[0] == opcode;
        }
    }

    public enum ScOpcodeType : byte
    {
        // smart contracts
        OP_CREATECONTRACT = 0xc0,
        OP_CALLCONTRACT = 0xc1,
        OP_SPEND = 0xc2,
        OP_INTERNALCONTRACTTRANSFER = 0xc3
    }
}