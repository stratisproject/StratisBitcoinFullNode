using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.ReflectionExecutor.Exceptions;
using Stratis.SmartContracts.ReflectionExecutor.Serialization;

namespace Stratis.SmartContracts.ReflectionExecutor
{
    /// <summary>
    /// This class holds execution code and other information related to smart contact.
    /// <para>
    /// The data should be serialized in this order:
    /// <list>
    /// <item><see cref="VmVersion"/></item>
    /// <item><see cref="OpCodeType"/></item>
    /// <item>If applicable <see cref="ContractAddress"/></item>
    /// <item>If applicable <see cref="MethodName"/></item>
    /// <item>If applicable <see cref="ContractExecutionCode"/></item>
    /// <item>If applicable <see cref="MethodParameters"/></item>
    /// <item><see cref="GasPrice"/></item>
    /// <item><see cref="GasLimit"/></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class SmartContractCarrier : ISmartContractCarrier
    {
        /// <summary>This is the contract's address.</summary>
        public uint160 ContractAddress { get; set; }

        /// <summary>The contract code that will be executed.</summary>
        public byte[] ContractExecutionCode { get; set; }

        /// <summary>The maximum amount of gas units that can spent to execute this contract.</summary>
        public Gas GasLimit { get; set; }

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

        /// <summary>The amount it costs per unit of gas to execute the contract.</summary>
        public ulong GasPrice { get; set; }

        /// <summary>The size of the bytes (int) we take to determine the length of the subsequent byte array.</summary>
        private const int intLength = sizeof(int);

        /// <summary>The method name of the contract that will be executed.</summary>
        public string MethodName { get; set; }

        /// <summary>The method parameters that will be passed to the <see cref="MethodName"/> when the contract is executed.</summary>
        public object[] MethodParameters { get; set; }

        /// <summary>A raw string representation of the method parameters, with escaped pipe and hash characters.</summary>
        private string methodParametersRaw;

        /// <summary>The index of the <see cref="TxOut"/> where the smart contract exists.</summary>
        public uint Nvout { get; set; }

        /// <summary>Specifies the smart contract operation to be done.</summary>
        public OpcodeType OpCodeType { get; set; }

        /// <summary>The serializer we use to serialize the method parameters.</summary>
        private readonly IMethodParameterSerializer serializer;

        /// <summary>The initiator of the the smart contract.</summary>
        public uint160 Sender { get; set; }

        /// <summary>The transaction hash that this smart contract references.</summary>
        public uint256 TransactionHash { get; set; }

        /// <summary>The value of the transaction's output (should be one UTXO).</summary>
        public ulong Value { get; set; }

        /// <summary>
        /// The virtual machine version we will use to decompile and execute the contract.
        /// </summary>
        public int VmVersion { get; set; }

        public SmartContractCarrier(IMethodParameterSerializer serializer)
        {
            this.serializer = serializer;
        }

        /// <summary>
        /// Instantiates a <see cref="OpcodeType.OP_CREATECONTRACT"/> smart contract carrier.
        /// </summary>
        public static SmartContractCarrier CreateContract(int vmVersion, byte[] contractExecutionCode, ulong gasPrice, Gas gasLimit)
        {
            var carrier = new SmartContractCarrier(new MethodParameterSerializer());
            carrier.VmVersion = vmVersion;
            carrier.OpCodeType = OpcodeType.OP_CREATECONTRACT;
            carrier.ContractExecutionCode = contractExecutionCode ?? throw new SmartContractCarrierException(nameof(contractExecutionCode) + " is null");
            carrier.GasPrice = gasPrice;
            carrier.GasLimit = gasLimit;
            return carrier;
        }

        /// <summary>
        /// Instantiates a <see cref="OpcodeType.OP_CREATECONTRACT"/> smart contract carrier with parameters.
        /// </summary>
        public static SmartContractCarrier CreateContract(int vmVersion, byte[] contractExecutionCode, ulong gasPrice, Gas gasLimit, string[] methodParameters)
        {
            SmartContractCarrier carrier = CreateContract(vmVersion, contractExecutionCode, gasPrice, gasLimit);
            carrier.WithParameters(methodParameters);
            return carrier;
        }

        /// <summary>
        /// Instantiates a <see cref="OpcodeType.OP_CALLCONTRACT"/> smart contract carrier with parameters.
        /// </summary>
        public static SmartContractCarrier CallContract(int vmVersion, uint160 contractAddress, string methodName, ulong gasPrice, Gas gasLimit, string[] methodParameters)
        {
            SmartContractCarrier carrier = CallContract(vmVersion, contractAddress, methodName, gasPrice, gasLimit);
            carrier.WithParameters(methodParameters);
            return carrier;
        }

        /// <summary>
        /// Instantiates a <see cref="OpcodeType.OP_CALLCONTRACT"/> smart contract carrier.
        /// </summary>
        public static SmartContractCarrier CallContract(int vmVersion, uint160 contractAddress, string methodName, ulong gasPrice, Gas gasLimit)
        {
            var carrier = new SmartContractCarrier(new MethodParameterSerializer());
            carrier.VmVersion = vmVersion;
            carrier.OpCodeType = OpcodeType.OP_CALLCONTRACT;
            carrier.ContractAddress = contractAddress;
            carrier.MethodName = methodName ?? throw new SmartContractCarrierException(nameof(methodName) + " is null");
            carrier.GasPrice = gasPrice;
            carrier.GasLimit = gasLimit;
            return carrier;
        }

        /// <summary>
        /// Create this carrier with method parameters.
        /// </summary>
        /// <param name="methodParameters">A string array representation of the method parameters.</param>
        private void WithParameters(string[] methodParameters)
        {
            if (this.OpCodeType == OpcodeType.OP_CALLCONTRACT && string.IsNullOrEmpty(this.MethodName))
                throw new SmartContractCarrierException(nameof(this.MethodName) + " must be supplied before specifying method parameters.");

            if (methodParameters == null)
                throw new SmartContractCarrierException(nameof(methodParameters) + " cannot be null.");

            if (methodParameters.Length == 0)
                throw new SmartContractCarrierException(nameof(methodParameters) + " length is 0.");

            this.methodParametersRaw = this.serializer.ToRaw(methodParameters);
            this.MethodParameters = this.serializer.ToObjects(this.methodParametersRaw);
        }

        /// <summary>
        /// Serialize the smart contract execution code and other related information.
        /// </summary>
        public byte[] Serialize()
        {
            var bytes = new List<byte>();
            bytes.Add((byte) this.OpCodeType);
            bytes.AddRange(this.PrefixLength(BitConverter.GetBytes(this.VmVersion)));

            if (this.OpCodeType == OpcodeType.OP_CALLCONTRACT)
            {
                bytes.AddRange(this.PrefixLength(this.ContractAddress.ToBytes()));
                bytes.AddRange(this.PrefixLength(Encoding.UTF8.GetBytes(this.MethodName)));
            }

            if (this.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                bytes.AddRange(this.PrefixLength(this.ContractExecutionCode));

            if (!string.IsNullOrEmpty(this.methodParametersRaw) && this.MethodParameters.Length > 0)
                bytes.AddRange(this.PrefixLength(this.serializer.ToBytes(this.methodParametersRaw)));
            else
                bytes.AddRange(BitConverter.GetBytes(0));

            bytes.AddRange(this.PrefixLength(BitConverter.GetBytes(this.GasPrice)));
            bytes.AddRange(this.PrefixLength(BitConverter.GetBytes(this.GasLimit)));

            return bytes.ToArray();
        }

        /// <summary>
        /// Prefixes the byte array with the length of the array that follows.
        /// </summary>
        private byte[] PrefixLength(byte[] toPrefix)
        {
            var prefixedBytes = new List<byte>();
            prefixedBytes.AddRange(BitConverter.GetBytes(toPrefix.Length));
            prefixedBytes.AddRange(toPrefix);
            return prefixedBytes.ToArray();
        }
    }

    public enum SmartContractCarrierDataType
    {
        Bool = 1,
        Byte,
        ByteArray,
        Char,
        SByte,
        Short,
        String,
        UInt,
        UInt160,
        ULong,
        Address
    }
}