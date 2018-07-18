using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    public sealed class SmartContractScriptAddressReader : IScriptAddressReader
    {
        private readonly IScriptAddressReader baseAddressReader;

        public SmartContractScriptAddressReader(IScriptAddressReader addressReader)
        {
            this.baseAddressReader = addressReader;
        }

        public string GetAddressFromScriptPubKey(Network network, Script script)
        {
            if (script.IsSmartContractCreate() || script.IsSmartContractCall())
            {
                CallData carrier = SmartContractCarrier.Deserialize(script);
                return carrier.ContractAddress?.ToAddress(network);
            }

            return this.baseAddressReader.GetAddressFromScriptPubKey(network, script);
        }
    }
}