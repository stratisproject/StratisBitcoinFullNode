namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Smart contract transaction data for a contract creation
    /// </summary>
    public class CreateData : ICreateData
    {
        public CreateData(Gas gasLimit, byte[] code, object[] methodParameters = null)
        {
            this.GasLimit = gasLimit;
            this.ContractExecutionCode = code;
            this.MethodParameters = methodParameters;
        }

        public object[] MethodParameters { get; }
        public Gas GasLimit { get; }
        public byte[] ContractExecutionCode { get; }
    }
}