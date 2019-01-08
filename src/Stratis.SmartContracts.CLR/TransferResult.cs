namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Returned when making a transfer from a smart contract.
    /// </summary>
    public sealed class TransferResult : ITransferResult
    {
        /// <summary>The return value of the method called.</summary>
        public object ReturnValue { get; private set; }

        /// <summary>Whether execution of the contract method was successful.</summary>
        public bool Success { get; }

        private TransferResult(bool success)
        {
            this.Success = success;
        }

        /// <summary>
        /// Constructs a result when the transfer is a funds transfer only and no return value is expected.
        /// </summary>
        public static TransferResult Empty()
        {
            return new TransferResult(true);
        }

        /// <summary>
        /// Constructs a result when a transfer from a contract failed.
        /// </summary>
        public static TransferResult Failed()
        {
            return new TransferResult(false);
        }

        /// <summary>
        /// Constructs a result when a transfer from a contract succeeded and a return value is expected.
        /// </summary>
        /// <param name="returnValue">The object that was returned from the executor.</param>
        public static TransferResult Transferred(object returnValue)
        {
            var result = new TransferResult(true)
            {
                ReturnValue = returnValue
            };

            return result;
        }
    }
}