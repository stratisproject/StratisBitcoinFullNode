using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Fields that are serialized and sent as data with a smart contract transaction
    /// </summary>
    public class ContractTxData
    {
        /// <summary>
        /// Creates a ContractTxData object for a method invocation
        /// </summary>
        public ContractTxData(int vmVersion, ulong gasPrice, Gas gasLimit, uint160 contractAddress,
            string method, object[] methodParameters = null)
        {
            this.OpCodeType = (byte) ScOpcodeType.OP_CALLCONTRACT;
            this.VmVersion = vmVersion;
            this.GasPrice = gasPrice;
            this.GasLimit = gasLimit;
            this.ContractAddress = contractAddress;
            this.MethodName = method;
            this.MethodParameters = methodParameters;
            this.ContractExecutionCode = new byte[0];
        }

        /// <summary>
        /// Creates a ContractTxData for contract creation
        /// </summary>
        public ContractTxData(int vmVersion, ulong gasPrice, Gas gasLimit, byte[] code,
            object[] methodParameters = null)
        {
            this.OpCodeType = (byte)ScOpcodeType.OP_CREATECONTRACT;
            this.VmVersion = vmVersion;
            this.GasPrice = gasPrice;
            this.GasLimit = gasLimit;
            this.ContractExecutionCode = code;
            this.MethodName = "";
            this.MethodParameters = methodParameters;
            this.ContractAddress = uint160.Zero;
        }

        /// <summary>The method name of the contract that will be executed.</summary>
        public string MethodName { get; }

        public object[] MethodParameters { get; }

        public uint160 ContractAddress { get; }

        /// <summary>The maximum amount of gas units that can spent to execute this contract.</summary>
        public Gas GasLimit { get; }

        /// <summary>The amount it costs per unit of gas to execute the contract.</summary>
        public ulong GasPrice { get; }

        /// <summary>
        /// The virtual machine version we will use to decompile and execute the contract.
        /// </summary>
        public int VmVersion { get; }

        /// <summary>Specifies the smart contract operation to be done.</summary>
        public byte OpCodeType { get; }

        /// <summary>The contract code that will be executed.</summary>
        public byte[] ContractExecutionCode { get; }

        /// <summary>The maximum cost (in satoshi) the contract can spend.</summary>
        public ulong GasCostBudget
        {
            get
            {
                checked
                {
                    return this.GasPrice * this.GasLimit;
                }
            }
        }

        /// <summary>Whether this data represents a contract creation.</summary>
        public bool IsCreateContract => this.OpCodeType == (byte) ScOpcodeType.OP_CREATECONTRACT;
    }
}