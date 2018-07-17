namespace Stratis.SmartContracts
{
    /// <summary>
    /// If the address of here the funds should be transferred to, is in fact a contract, the executor needs to be supplied with the details
    /// of said contract (method name and paramters) to execute.
    /// </summary>
    public sealed class TransferFundsToContract
    {
        /// <summary>
        /// The name of the contract method to execute.
        /// </summary>
        public string ContractMethodName { get; set; }

        /// <summary>
        /// If required, the method parameters that will be passed to the <see cref="ContractMethodName"/>.
        /// </summary>
        public object[] MethodParameters { get; set; }
    }
}