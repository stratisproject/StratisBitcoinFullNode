using NBitcoin;
using TracerAttributes;

namespace Stratis.SmartContracts.Core
{
    public static class SmartContractScript
    {
        [NoTrace]
        public static bool IsSmartContractExec(this Script script)
        {
            return TestFirstByte(script, new byte[]{ (byte)ScOpcodeType.OP_CALLCONTRACT, (byte)ScOpcodeType.OP_CREATECONTRACT });
        }

        [NoTrace]
        public static bool IsSmartContractCall(this Script script)
        {
            return TestFirstByte(script,  new byte[]{(byte)ScOpcodeType.OP_CALLCONTRACT });
        }

        [NoTrace]
        public static bool IsSmartContractCreate(this Script script)
        {
            return TestFirstByte(script, new byte[] {(byte)ScOpcodeType.OP_CREATECONTRACT });
        }

        [NoTrace]
        public static bool IsSmartContractSpend(this Script script)
        {
            return TestFirstByte(script, new byte[] {(byte)ScOpcodeType.OP_SPEND });
        }

        [NoTrace]
        public static bool IsSmartContractInternalCall(this Script script)
        {
            return TestFirstByte(script, new byte[]{(byte) ScOpcodeType.OP_INTERNALCONTRACTTRANSFER });
        }

        /// <summary>
        /// Tests whether a script starts with any of the given opcodes.
        /// </summary>
        /// <param name="script">The script to test.</param>
        /// <param name="opcodes">A list of opcodes in byte form to test against.</param>
        /// <returns>True if the script starts with one of the given opcodes.</returns>
        [NoTrace]
        private static bool TestFirstByte(Script script, byte[] opcodes)
        {
            // The bytes aren't touched and don't leave this method so use unsafe for performance (no array copy).
            var scriptBytes = script.ToBytes(true);

            if (scriptBytes.Length == 0)
                return false;

            foreach (byte opcode in opcodes)
            {
                if (scriptBytes[0] == opcode)
                    return true;
            }

            return false;
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