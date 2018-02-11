using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NBitcoin;
using Stratis.SmartContracts.Hashing;

namespace Stratis.SmartContracts
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
    /// <item><see cref="GasPrice"/></item>
    /// <item><see cref="GasLimit"/></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class SmartContractCarrier
    {
        /// <summary>The contract code that will be executed.</summary>
        public byte[] ContractExecutionCode { get; private set; }

        /// <summary>The maximum amount of satoshi that can spent to execute this contract.</summary>
        public ulong GasLimit { get; private set; }

        /// <summary>What this contract costs to execute (in satoshi).</summary>
        public ulong GasPrice { get; private set; }

        /// <summary>The size of the bytes (int) we take to determine the length of the subsequent byte array.</summary>
        private const int intLength = sizeof(int);

        /// <summary>The method name of the contract that will be executed.</summary>
        public string MethodName { get; private set; }

        private string methodParameters;
        /// <summary>The method parameters that will be passed to the <see cref="MethodName"/> when the contract is executed.</summary>
        public string[] MethodParameters
        {
            get
            {
                if (string.IsNullOrEmpty(this.methodParameters))
                    return null;

                return Regex.Split(this.methodParameters, @"(?<!(?<!\\)*\\)\|").Select(parameter => parameter.Replace(@"\|", "|")).ToArray();
            }
        }

        /// <summary>TODO : Add description.</summary>
        public uint Nvout { get; set; }

        /// <summary>Specifies the smart contract operation to be done.</summary>
        public readonly OpcodeType OpCodeType;

        /// <summary>The initiator of the the smart contract.</summary>
        public uint160 Sender { get; set; }

        /// <summary>The transaction hash that this smart contract references.</summary>
        public uint256 TransactionHash { get; private set; }

        /// <summary>Teh value of transaction's output.</summary>
        public ulong TxOutValue { get; private set; }

        /// <summary>
        /// The virtual machine version we will use to decompile and execute the contract.
        /// </summary>
        public readonly uint VmVersion;

        /// <summary>TODO : Add description.</summary>
        public ulong TotalGas
        {
            get
            {
                return this.GasPrice * this.GasLimit;
            }
        }

        public uint160 To { get; set; }

        private SmartContractCarrier(uint vmVersion, OpcodeType opCodeType)
        {
            // TODO: Add null/valid checks for 
            // vmVersion
            // opCodeType

            this.VmVersion = vmVersion;
            this.OpCodeType = opCodeType;
        }

        public static SmartContractCarrier CreateContract(uint vmVersion, byte[] contractExecutionCode, ulong gasPrice, ulong gasLimit)
        {
            // TODO: Add null/valid checks for 
            // contractExecutionCode
            // gasPrice
            // gasLimit

            var carrier = new SmartContractCarrier(vmVersion, OpcodeType.OP_CREATECONTRACT);
            carrier.ContractExecutionCode = contractExecutionCode;
            carrier.GasPrice = gasPrice;
            carrier.GasLimit = gasLimit;
            return carrier;
        }

        public static SmartContractCarrier CallContract(uint vmVersion, uint160 to, string methodName, ulong gasPrice, ulong gasLimit)
        {
            // TODO: Add null/valid checks for 
            // to
            // methodName
            // gasPrice
            // gasLimit

            var carrier = new SmartContractCarrier(vmVersion, OpcodeType.OP_CALLCONTRACT);
            carrier.To = to;
            carrier.MethodName = methodName;
            carrier.GasPrice = gasPrice;
            carrier.GasLimit = gasLimit;
            return carrier;
        }

        /// <summary>
        /// Create this carrier with method parameters.
        /// </summary>
        /// <param name="methodParameters">A string array representation of the method parameters.</param>
        public SmartContractCarrier WithParameters(string[] methodParameters)
        {
            //TODO: SmartContractCarrierException?
            if (this.OpCodeType == OpcodeType.OP_CALLCONTRACT && string.IsNullOrEmpty(this.MethodName))
                throw new Exception(nameof(this.MethodName) + " must be supplied before specifying method parameters.");

            // TODO: SmartContractCarrierException?
            if (methodParameters == null)
                throw new Exception(nameof(methodParameters) + " cannot be null.");

            if (methodParameters.Length == 0)
                return this;

            this.methodParameters = string.Join('|', methodParameters.Select(parameter => parameter.Replace("|", @"\|")));

            return this;
        }

        /// <summary>
        /// Deserializes the smart contract execution code and other related information.
        /// </summary>
        public static SmartContractCarrier Deserialize(Transaction transaction, TxOut smartContractTxOut)
        {
            byte[] smartContractBytes = smartContractTxOut.ScriptPubKey.ToBytes();

            var byteCursor = 0;
            var takeLength = 0;

            var vmVersion = Deserialize<uint>(smartContractBytes, ref byteCursor, ref takeLength);
            var opCodeType = (OpcodeType)Deserialize<short>(smartContractBytes, ref byteCursor, ref takeLength);

            var smartContractCarrier = new SmartContractCarrier(vmVersion, opCodeType);
            if (smartContractCarrier.OpCodeType == OpcodeType.OP_CALLCONTRACT)
            {
                smartContractCarrier.To = Deserialize<uint160>(smartContractBytes, ref byteCursor, ref takeLength);
                smartContractCarrier.MethodName = Deserialize<string>(smartContractBytes, ref byteCursor, ref takeLength);
            }

            if (smartContractCarrier.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                smartContractCarrier.ContractExecutionCode = Deserialize<byte[]>(smartContractBytes, ref byteCursor, ref takeLength);

            smartContractCarrier.methodParameters = Deserialize<string>(smartContractBytes, ref byteCursor, ref takeLength);
            smartContractCarrier.Nvout = Convert.ToUInt32(transaction.Outputs.IndexOf(smartContractTxOut));
            smartContractCarrier.GasPrice = Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
            smartContractCarrier.GasLimit = Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
            smartContractCarrier.TransactionHash = transaction.GetHash();
            smartContractCarrier.TxOutValue = smartContractTxOut.Value;

            return smartContractCarrier;
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
        /// Serializes the smart contract execution code and other related information.
        /// </summary>
        public byte[] Serialize()
        {
            var bytes = new List<byte>();
            bytes.AddRange(PrefixLength(BitConverter.GetBytes(this.VmVersion)));
            bytes.AddRange(PrefixLength(BitConverter.GetBytes((byte)this.OpCodeType)));

            if (this.OpCodeType == OpcodeType.OP_CALLCONTRACT)
            {
                bytes.AddRange(PrefixLength(this.To.ToBytes()));
                bytes.AddRange(PrefixLength(Encoding.UTF8.GetBytes(this.MethodName)));
            }

            if (this.OpCodeType == OpcodeType.OP_CREATECONTRACT)
                bytes.AddRange(PrefixLength(this.ContractExecutionCode));

            if (!string.IsNullOrEmpty(this.methodParameters))
                bytes.AddRange(PrefixLength(Encoding.UTF8.GetBytes(this.methodParameters)));
            else
                bytes.AddRange(BitConverter.GetBytes(0));

            bytes.AddRange(PrefixLength(BitConverter.GetBytes(this.GasPrice)));
            bytes.AddRange(PrefixLength(BitConverter.GetBytes(this.GasLimit)));

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

        /// <summary>
        /// TODO: Could put this on the 'Transaction' object in NBitcoin if allowed
        /// </summary>
        public uint160 GetNewContractAddress()
        {
            return new uint160(HashHelper.Keccak256(this.TransactionHash.ToBytes()).Take(20).ToArray());
        }
    }

    public enum SmartContractCarrierDataType
    {
        Bool,
        Byte,
        Char,
        ByteArray,
        SByte,
        Short,
        String,
        UInt,
        UInt160,
        ULong
    }
}