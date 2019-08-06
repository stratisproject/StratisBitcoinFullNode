using NBitcoin;
using TracerAttributes;

namespace Stratis.SmartContracts.Core
{
    public static class SmartContractScript
    {
        [NoTrace]
        public static bool IsSmartContractExec(this Script script)
        {
            return script.IsSmartContractCall() || script.IsSmartContractCreate();
        }

        [NoTrace]
        public static bool IsSmartContractCall(this Script script)
        {
            return TestFirstByte(script, (byte)ScOpcodeType.OP_CALLCONTRACT);
        }

        [NoTrace]
        public static bool IsSmartContractCreate(this Script script)
        {
            return TestFirstByte(script, (byte)ScOpcodeType.OP_CREATECONTRACT);
        }

        [NoTrace]
        public static bool IsSmartContractSpend(this Script script)
        {
            return TestFirstByte(script, (byte)ScOpcodeType.OP_SPEND);

        }

        [NoTrace]
        public static bool IsSmartContractInternalCall(this Script script)
        {
            return TestFirstByte(script, (byte) ScOpcodeType.OP_INTERNALCONTRACTTRANSFER);
        }

        [NoTrace]
        private static bool TestFirstByte(Script script, byte opcode)
        {
            byte[] scriptBytes = script.ToBytes(true);

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