using NBitcoin;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Represents an external contract creation message. Occurs when a transaction is received that contains contract creation data.
    /// </summary>
    public class ExternalCreateMessage : BaseMessage
    {
        public ExternalCreateMessage(uint160 from, ulong amount, RuntimeObserver.Gas gasLimit, byte[] code, object[] parameters)
            : base(from, amount, gasLimit)
        {
            this.Code = code;
            this.Parameters = parameters;
        }

        /// <summary>
        /// The code of the contract being created.
        /// </summary>
        public byte[] Code { get; }

        /// <summary>
        /// The parameters to use when creating the contract.
        /// </summary>
        public object[] Parameters { get; }
    }
}