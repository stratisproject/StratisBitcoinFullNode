using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Information about the current state of the blockchain that can be accessed in the virtual machine.
    /// </summary>
    internal class SmartContractExecutionContext
    {
        public uint160 ContractAddress { get; set; }
        public uint160 CallerAddress { get; set; }
        public uint160 CoinbaseAddress { get; set; }

        public ulong CallValue { get; set; }
        public ulong GasPrice { get; set; }

        public ulong BlockNumber { get; set; }
        public ulong Difficulty { get; set; }
        public ulong GasLimit { get; set; }

        public string ContractTypeName { get; set; }
        public string ContractMethod { get; set; }
        public object[] Parameters { get; private set; }

        public SmartContractExecutionContext(SmartContractCarrier smartContractCarrier, ulong blockNumber, uint160 coinbaseAddress, string contractTypeName, ulong difficulty)
        {
            this.BlockNumber = blockNumber;
            this.CallerAddress = smartContractCarrier.Sender;
            this.CallValue = smartContractCarrier.TxOutValue;
            this.CoinbaseAddress = coinbaseAddress;
            this.ContractAddress = smartContractCarrier.To;
            this.ContractMethod = smartContractCarrier.MethodName;
            this.ContractTypeName = contractTypeName;
            this.Difficulty = difficulty;
            this.GasLimit = smartContractCarrier.GasLimit;
            this.GasPrice = smartContractCarrier.GasPrice;

            if (smartContractCarrier.MethodParameters != null && smartContractCarrier.MethodParameters.Length > 0)
                ProcessMethodParamters(smartContractCarrier.MethodParameters);
        }

        private void ProcessMethodParamters(string[] methodParameters)
        {
            var processedParameters = new List<object>();
            foreach (var parameter in methodParameters)
            {
                string[] parameterSignature = parameter.Split('#');
                if (parameterSignature[0] == "int")
                    processedParameters.Add(int.Parse(parameterSignature[1]));
            }

            this.Parameters = processedParameters.ToArray();
        }
    }
}