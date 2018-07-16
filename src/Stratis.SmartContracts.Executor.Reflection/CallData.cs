using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public struct CallData
    {
        public CallData(byte opCodeType, int vmVersion, ulong gasPrice, Gas gasLimit, uint160 address,
            string method, string parameters = "")
        {
            this.OpCodeType = opCodeType;
            this.VmVersion = vmVersion;
            this.GasPrice = gasPrice;
            this.GasLimit = gasLimit;
            this.ContractAddress = address;
            this.MethodName = method;
            this.MethodParameters = parameters;
            this.ContractExecutionCode = new byte[0];
        }

        public CallData(byte opCodeType, int vmVersion, ulong gasPrice, Gas gasLimit, byte[] code, string parameters = "")
        {
            this.OpCodeType = opCodeType;
            this.VmVersion = vmVersion;
            this.GasPrice = gasPrice;
            this.GasLimit = gasLimit;
            this.MethodParameters = parameters;
            this.ContractExecutionCode = code;
            this.MethodName = "";
            this.ContractAddress = uint160.Zero;
        }

        /// <summary>The method name of the contract that will be executed.</summary>
        public string MethodName { get; }

        public string MethodParameters { get; }

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
    }
}