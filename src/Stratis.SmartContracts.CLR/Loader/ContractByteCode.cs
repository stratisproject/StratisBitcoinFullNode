namespace Stratis.SmartContracts.CLR.Loader
{
    /// <summary>
    /// Represents a contract's bytecode.
    /// </summary>
    public class ContractByteCode
    {
        public ContractByteCode(byte[] bytes)
        {
            this.Value = bytes;
        }

        public byte[] Value { get; }

        public static explicit operator ContractByteCode(byte[] bytes)
        {
            return new ContractByteCode(bytes);
        }
    }
}
