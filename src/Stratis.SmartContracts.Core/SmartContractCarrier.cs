using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.Serialization;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// This class holds execution code and other information related to smart contact.
    /// <para>
    /// The data should be serialized in this order:
    /// <list>
    /// <item><see cref="VmVersion"/></item>
    /// <item><see cref="OpCodeType"/></item>
    /// <item>If applicable <see cref="To"/></item>
    /// <item>If applicable <see cref="MethodName"/></item>
    /// <item>If applicable <see cref="ContractExecutionCode"/></item>
    /// <item>If applicable <see cref="MethodParameters"/></item>
    /// <item><see cref="GasUnitPrice"/></item>
    /// <item><see cref="GasLimit"/></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class SmartContractCarrier
    {
        /// <summary>The contract code that will be executed.</summary>
        public byte[] ContractExecutionCode { get; private set; }

        /// <summary>The maximum amount of satoshi that can spent to execute this contract.</summary>
        public Gas GasLimit { get; private set; }

        /// <summary>The maximum cost (in satoshi) the contract can spend.</summary>
        public ulong GasCostBudget
        {
            get { return this.GasUnitPrice * this.GasLimit; }
        }

        /// <summary>The amount it costs per unit of gas to execute the contract.</summary>
        public ulong GasUnitPrice { get; private set; }

        /// <summary>The size of the bytes (int) we take to determine the length of the subsequent byte array.</summary>
        private const int intLength = sizeof(int);

        /// <summary>The method name of the contract that will be executed.</summary>
        public string MethodName { get; private set; }

        /// <summary>The method parameters that will be passed to the <see cref="MethodName"/> when the contract is executed.</summary>
        public object[] MethodParameters { get; private set; }

        /// <summary>A raw string representation of the method parameters, with escaped pipe and hash characters.</summary>
        private string methodParametersRaw;

        /// <summary>The index of the <see cref="TxOut"/> where the smart contract exists.</summary>
        public uint Nvout { get; set; }

        /// <summary>Specifies the smart contract operation to be done.</summary>
        public OpcodeType OpCodeType { get; private set; }

        /// <summary>The serializer we use to serialize the method parameters.</summary>
        private readonly IMethodParameterSerializer serializer;

        /// <summary>The initiator of the the smart contract.</summary>
        public uint160 Sender { get; set; }

        /// <summary>The transaction hash that this smart contract references.</summary>
        public uint256 TransactionHash { get; private set; }

        /// <summary>The value of the transaction's output (should be one UTXO).</summary>
        public ulong TxOutValue { get; private set; }

        /// <summary>
        /// The virtual machine version we will use to decompile and execute the contract.
        /// </summary>
        public int VmVersion { get; private set; }

        /// <summary>This is the new contract's address.</summary>
        public uint160 To { get; set; }

        private SmartContractCarrier(IMethodParameterSerializer serializer)
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
            carrier.GasUnitPrice = gasPrice;
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
        public static SmartContractCarrier CallContract(int vmVersion, uint160 to, string methodName, ulong gasPrice, Gas gasLimit, string[] methodParameters)
        {
            SmartContractCarrier carrier = CallContract(vmVersion, to, methodName, gasPrice, gasLimit);
            carrier.WithParameters(methodParameters);
            return carrier;
        }

        /// <summary>
        /// Instantiates a <see cref="OpcodeType.OP_CALLCONTRACT"/> smart contract carrier.
        /// </summary>
        public static SmartContractCarrier CallContract(int vmVersion, uint160 to, string methodName, ulong gasPrice, Gas gasLimit)
        {
            var carrier = new SmartContractCarrier(new MethodParameterSerializer());
            carrier.VmVersion = vmVersion;
            carrier.OpCodeType = OpcodeType.OP_CALLCONTRACT;
            carrier.To = to;
            carrier.MethodName = methodName ?? throw new SmartContractCarrierException(nameof(methodName) + " is null");
            carrier.GasUnitPrice = gasPrice;
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
        /// Deserializes the smart contract execution code and other related information.
        /// </summary>
        public static SmartContractCarrier Deserialize(Transaction transaction, TxOut smartContractTxOut)
        {
            byte[] smartContractBytes = smartContractTxOut.ScriptPubKey.ToBytes();

            var byteCursor = 0;
            var takeLength = 0;

            var carrier = new SmartContractCarrier(new MethodParameterSerializer())
            {
                VmVersion = Deserialize<int>(smartContractBytes, ref byteCursor, ref takeLength),
                OpCodeType = (OpcodeType)Deserialize<short>(smartContractBytes, ref byteCursor, ref takeLength)
            };

            if (carrier.OpCodeType == OpcodeType.OP_CALLCONTRACT)
            {
                carrier.To = Deserialize<uint160>(smartContractBytes, ref byteCursor, ref takeLength);
                carrier.MethodName = Deserialize<string>(smartContractBytes, ref byteCursor, ref takeLength);
            }

            if (carrier.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                carrier.ContractExecutionCode = Deserialize<byte[]>(smartContractBytes, ref byteCursor, ref takeLength);

            var methodParameters = Deserialize<string>(smartContractBytes, ref byteCursor, ref takeLength);
            if (!string.IsNullOrEmpty(methodParameters))
                carrier.MethodParameters = carrier.serializer.ToObjects(methodParameters);

            carrier.Nvout = Convert.ToUInt32(transaction.Outputs.IndexOf(smartContractTxOut));
            carrier.GasUnitPrice = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
            carrier.GasLimit = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
            carrier.TransactionHash = transaction.GetHash();
            carrier.TxOutValue = smartContractTxOut.Value;

            return carrier;
        }

        private static T Deserialize<T>(byte[] smartContractBytes, ref int byteCursor, ref int takeLength)
        {
            takeLength = BitConverter.ToInt16(smartContractBytes.Skip(byteCursor).Take(intLength).ToArray(), 0);
            byteCursor += intLength;

            if (takeLength == 0)
                return default(T);

            object result = null;

            if (typeof(T) == typeof(bool))
                result = BitConverter.ToBoolean(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            if (typeof(T) == typeof(byte[]))
                result = smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray();

            if (typeof(T) == typeof(int))
                result = BitConverter.ToInt32(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            if (typeof(T) == typeof(short))
                result = BitConverter.ToInt16(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            if (typeof(T) == typeof(string))
                result = Encoding.UTF8.GetString(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray());

            if (typeof(T) == typeof(uint))
                result = BitConverter.ToUInt32(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            if (typeof(T) == typeof(uint160))
                result = new uint160(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray());

            if (typeof(T) == typeof(ulong))
                result = BitConverter.ToUInt64(smartContractBytes.Skip(byteCursor).Take(takeLength).ToArray(), 0);

            byteCursor += takeLength;

            return (T)result;
        }

        /// <summary>
        /// Serialize the smart contract execution code and other related information.
        /// </summary>
        public byte[] Serialize()
        {
            var bytes = new List<byte>();
            bytes.AddRange(this.PrefixLength(BitConverter.GetBytes(this.VmVersion)));
            bytes.AddRange(this.PrefixLength(BitConverter.GetBytes((byte)this.OpCodeType)));

            if (this.OpCodeType == OpcodeType.OP_CALLCONTRACT)
            {
                bytes.AddRange(this.PrefixLength(this.To.ToBytes()));
                bytes.AddRange(this.PrefixLength(Encoding.UTF8.GetBytes(this.MethodName)));
            }

            if (this.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                bytes.AddRange(this.PrefixLength(this.ContractExecutionCode));

            if (!string.IsNullOrEmpty(this.methodParametersRaw) && this.MethodParameters.Length > 0)
                bytes.AddRange(this.PrefixLength(this.serializer.ToBytes(this.methodParametersRaw)));
            else
                bytes.AddRange(BitConverter.GetBytes(0));

            bytes.AddRange(this.PrefixLength(BitConverter.GetBytes(this.GasUnitPrice)));
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