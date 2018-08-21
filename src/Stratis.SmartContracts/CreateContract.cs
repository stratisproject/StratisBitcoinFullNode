namespace Stratis.SmartContracts
{
    public sealed class CreateContract
    {
        /// <summary>
        /// If required, the method parameters that will be passed to the <see cref="ContractMethodName"/>.
        /// </summary>
        public object[] MethodParameters { get; set; }

        /// <summary>
        /// The amount of gas to spend in this contract call. If not set the protocol default will be used. 
        /// </summary>
        public ulong GasBudget { get; set; }
    }
}
