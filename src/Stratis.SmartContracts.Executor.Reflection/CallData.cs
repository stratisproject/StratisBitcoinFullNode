using NBitcoin;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public struct CallData
    {
        public CallData(int vmVersion, ulong gasPrice, Gas gasLimit, uint160 address,
            string method, string rawParameters = "", object[] methodParameters = null)
        {
            this.OpCodeType = (byte) ScOpcodeType.OP_CALLCONTRACT;
            this.VmVersion = vmVersion;
            this.GasPrice = gasPrice;
            this.GasLimit = gasLimit;
            this.ContractAddress = address;
            this.MethodName = method;
            this.MethodParametersRaw = rawParameters;            
            this.MethodParameters = methodParameters;
            this.ContractExecutionCode = new byte[0];
        }

        public CallData(int vmVersion, ulong gasPrice, Gas gasLimit, byte[] code, string rawParameters = "",
            object[] methodParameters = null)
        {
            this.OpCodeType = (byte)ScOpcodeType.OP_CREATECONTRACT;
            this.VmVersion = vmVersion;
            this.GasPrice = gasPrice;
            this.GasLimit = gasLimit;
            this.ContractExecutionCode = code;
            this.MethodName = "";
            this.MethodParametersRaw = rawParameters;
            this.MethodParameters = methodParameters;
            this.ContractAddress = uint160.Zero;
        }

        /// <summary>The method name of the contract that will be executed.</summary>
        public string MethodName { get; }

        public string MethodParametersRaw { get; }

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
    }
}