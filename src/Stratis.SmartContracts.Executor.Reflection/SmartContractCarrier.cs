using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
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
    public sealed class SmartContractCarrier
    {
        /// <summary>The contract data provided with the transaction</summary>
        public ContractTxData ContractTxData { get; set; }

        /// <summary>The method parameters that will be passed to the <see cref="MethodName"/> when the contract is executed.</summary>
        public object[] MethodParameters { get; private set; }

        /// <summary>The index of the <see cref="TxOut"/> where the smart contract exists.</summary>
        public uint Nvout { get; private set; }

        /// <summary>The serializer we use to serialize the method parameters.</summary>
        private readonly IMethodParameterSerializer serializer;

        /// <summary>The initiator of the the smart contract. TODO: Make set private.</summary>
        public uint160 Sender { get; set; }

        /// <summary>The transaction hash that this smart contract references.</summary>
        public uint256 TransactionHash { get; private set; }

        /// <summary>The value of the transaction's output (should be one UTXO).</summary>
        public ulong Value { get; private set; }

        public SmartContractCarrier(IMethodParameterSerializer serializer)
        {
            this.serializer = serializer;
        }

        /// <summary>
        /// Instantiates a <see cref="ScOpcodeType.OP_CREATECONTRACT"/> smart contract carrier.
        /// </summary>
        public static SmartContractCarrier CreateContract(int vmVersion, byte[] contractExecutionCode, ulong gasPrice,
            Gas gasLimit, string[] methodParameters = null)
        {
            if (contractExecutionCode == null)
                throw new SmartContractCarrierException(nameof(contractExecutionCode) + " is null");

            var serializer = new MethodParameterSerializer();
            string methodParams = GetMethodParams(serializer, methodParameters);

            var callData = new ContractTxData(vmVersion, gasPrice, gasLimit, contractExecutionCode, methodParams);

            var carrier = new SmartContractCarrier(new MethodParameterSerializer());
            carrier.ContractTxData = callData;

            if (!string.IsNullOrWhiteSpace(methodParams))
                carrier.MethodParameters = serializer.ToObjects(methodParams);
            return carrier;
        }

        /// <summary>
        /// Instantiates a <see cref="ScOpcodeType.OP_CALLCONTRACT"/> smart contract carrier.
        /// </summary>
        public static SmartContractCarrier CallContract(int vmVersion, uint160 contractAddress, string methodName, ulong gasPrice, Gas gasLimit, string[] methodParameters = null)
        {
            if (string.IsNullOrWhiteSpace(methodName))
                throw new SmartContractCarrierException(nameof(methodName) + " is null or empty");

            var serializer = new MethodParameterSerializer();
            string methodParams = GetMethodParams(serializer, methodParameters);
            var carrier = new SmartContractCarrier(new MethodParameterSerializer());
            carrier.ContractTxData = new ContractTxData(vmVersion, gasPrice, gasLimit, contractAddress, methodName, methodParams);
            
            if (!string.IsNullOrWhiteSpace(methodParams))
                carrier.MethodParameters = serializer.ToObjects(methodParams);

            return carrier;
        }

        private static string GetMethodParams(IMethodParameterSerializer serializer, string[] methodParameters)
        {
            var methodParams = methodParameters != null && methodParameters.Length > 0
                ? serializer.ToRaw(methodParameters)
                : "";

            return methodParams;
        }

        /// <summary>
        /// Serialize the smart contract execution code and other related information.
        /// </summary>
        public byte[] Serialize()
        {
            var bytes = new List<byte>
            {
                this.ContractTxData.OpCodeType
            };

            bytes.AddRange(this.PrefixLength(BitConverter.GetBytes(this.ContractTxData.VmVersion)));
            bytes.AddRange(this.PrefixLength(BitConverter.GetBytes(this.ContractTxData.GasPrice)));
            bytes.AddRange(this.PrefixLength(BitConverter.GetBytes(this.ContractTxData.GasLimit)));

            if (this.ContractTxData.OpCodeType == (byte) ScOpcodeType.OP_CALLCONTRACT)
            {
                bytes.AddRange(this.PrefixLength(this.ContractTxData.ContractAddress.ToBytes()));
                bytes.AddRange(this.PrefixLength(Encoding.UTF8.GetBytes(this.ContractTxData.MethodName)));
            }

            if (this.ContractTxData.OpCodeType == (byte) ScOpcodeType.OP_CREATECONTRACT)
                bytes.AddRange(this.PrefixLength(this.ContractTxData.ContractExecutionCode));

            if (!string.IsNullOrWhiteSpace(this.ContractTxData.MethodParametersRaw) && this.MethodParameters.Length > 0)
                bytes.AddRange(this.PrefixLength(this.serializer.ToBytes(this.ContractTxData.MethodParametersRaw)));
            else
                bytes.AddRange(BitConverter.GetBytes(0));

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