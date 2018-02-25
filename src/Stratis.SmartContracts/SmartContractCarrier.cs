using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NBitcoin;

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

        private string methodParameters;
        /// <summary>The method parameters that will be passed to the <see cref="MethodName"/> when the contract is executed.</summary>
        public object[] MethodParameters { get; private set; }

        /// <summary>The index of the <see cref="TxOut"/> where the smart contract exists.</summary>
        public uint Nvout { get; set; }

        /// <summary>Specifies the smart contract operation to be done.</summary>
        public readonly OpcodeType OpCodeType;

        /// <summary>The initiator of the the smart contract.</summary>
        public uint160 Sender { get; set; }

        /// <summary>The transaction hash that this smart contract references.</summary>
        public uint256 TransactionHash { get; private set; }

        /// <summary>The value of the transaction's output (should be one UTXO).</summary>
        public ulong TxOutValue { get; private set; }

        /// <summary>
        /// The virtual machine version we will use to decompile and execute the contract.
        /// </summary>
        public readonly uint VmVersion;

        /// <summary>This is the new contract's address.</summary>
        public uint160 To { get; set; }

        private SmartContractCarrier(uint vmVersion, OpcodeType opCodeType)
        {
            // TODO: Add null/valid checks for 
            // vmVersion
            // opCodeType

            this.VmVersion = vmVersion;
            this.OpCodeType = opCodeType;
        }

        public static SmartContractCarrier CreateContract(uint vmVersion, byte[] contractExecutionCode, ulong gasPrice, Gas gasLimit)
        {
            // TODO: Add null/valid checks for 
            // contractExecutionCode
            // gasPrice
            // gasLimit

            var carrier = new SmartContractCarrier(vmVersion, OpcodeType.OP_CREATECONTRACT);
            carrier.ContractExecutionCode = contractExecutionCode;
            carrier.GasUnitPrice = gasPrice;
            carrier.GasLimit = gasLimit;
            return carrier;
        }

        public static SmartContractCarrier CallContract(uint vmVersion, uint160 to, string methodName, ulong gasPrice, Gas gasLimit)
        {
            // TODO: Add null/valid checks for 
            // to
            // methodName
            // gasPrice
            // gasLimit

            var carrier = new SmartContractCarrier(vmVersion, OpcodeType.OP_CALLCONTRACT);
            carrier.To = to;
            carrier.MethodName = methodName;
            carrier.GasUnitPrice = gasPrice;
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

            IEnumerable<string> processedPipes = methodParameters.Select(parameter => parameter = parameter.Replace("|", @"\|"));

            IEnumerable<string> processedHashes = processedPipes.Select(parameter =>
            {

                // This delegate splits the string by the hash character.
                // 
                // If the split array is longer than 2 then we need to 
                // reconstruct the parameter by escaping all hashes
                // after the first one.
                // 
                // Once this is done, prepend the string with the data type,
                // which is an integer representation of SmartContractCarrierDataType,
                // as well as a hash, so that it can be split again upon deserialization.
                //
                // I.e. 3#dcg#5d# will split into 3 / dcg / 5d
                // and then dcg / fd will be reconstructed to dcg\\#5d\\# and
                // 3# prepended to make 3#dcg\\#5d\\#

                string[] hashes = parameter.Split('#');
                if (hashes.Length == 2)
                    return parameter;

                var reconstructed = new List<string>();
                for (int i = 1; i < hashes.Length; i++)
                {
                    reconstructed.Add(hashes[i]);
                }

                var result = string.Join('#', reconstructed).Replace("#", @"\#");
                return hashes[0].Insert(hashes[0].Length, "#" + result);
            });

            this.methodParameters = string.Join('|', processedHashes);
            this.MethodParameters = ConstructMethodParameters(this.methodParameters);

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

            if (!string.IsNullOrEmpty(smartContractCarrier.methodParameters))
                smartContractCarrier.MethodParameters = ConstructMethodParameters(smartContractCarrier.methodParameters);

            smartContractCarrier.Nvout = Convert.ToUInt32(transaction.Outputs.IndexOf(smartContractTxOut));
            smartContractCarrier.GasUnitPrice = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
            smartContractCarrier.GasLimit = (Gas)Deserialize<ulong>(smartContractBytes, ref byteCursor, ref takeLength);
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
        /// Parses the method parameters, passed in as a string[] and reconstructs it as object[].
        /// </summary>
        private static object[] ConstructMethodParameters(string methodParameters)
        {
            string[] splitParameters = Regex.Split(methodParameters, @"(?<!(?<!\\)*\\)\|").Select(parameter => parameter.Replace(@"\|", "|")).ToArray();

            var processedParameters = new List<object>();
            foreach (var parameter in splitParameters)
            {
                string[] parameterSignature = Regex.Split(parameter, @"(?<!(?<!\\)*\\)\#").Select(hashparameter => hashparameter.Replace(@"\#", "#")).ToArray();

                if (parameterSignature[0] == "1")
                    processedParameters.Add(bool.Parse(parameterSignature[1]));

                else if (parameterSignature[0] == "2")
                    processedParameters.Add(Convert.ToByte(parameterSignature[1]));

                else if (parameterSignature[0] == "3")
                    processedParameters.Add(Encoding.UTF8.GetBytes(parameterSignature[1]));

                else if (parameterSignature[0] == "4")
                    processedParameters.Add(char.Parse(parameterSignature[1]));

                else if (parameterSignature[0] == "5")
                    processedParameters.Add(Convert.ToSByte(parameterSignature[1]));

                else if (parameterSignature[0] == "6")
                    processedParameters.Add(int.Parse(parameterSignature[1]));

                else if (parameterSignature[0] == "7")
                    processedParameters.Add(parameterSignature[1]);

                else if (parameterSignature[0] == "8")
                    processedParameters.Add(uint.Parse(parameterSignature[1]));

                else if (parameterSignature[0] == "9")
                    processedParameters.Add(new uint160(parameterSignature[1]));

                else if (parameterSignature[0] == "10")
                    processedParameters.Add(ulong.Parse(parameterSignature[1]));

                else if (parameterSignature[0] == "11")
                    processedParameters.Add(new Address(parameterSignature[1]));

                else
                    throw new Exception(string.Format("{0} is not supported.", parameterSignature[0]));
            }

            return processedParameters.ToArray();
        }

        /// <summary>
        /// Serialize the smart contract execution code and other related information.
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

            bytes.AddRange(PrefixLength(BitConverter.GetBytes(this.GasUnitPrice)));
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