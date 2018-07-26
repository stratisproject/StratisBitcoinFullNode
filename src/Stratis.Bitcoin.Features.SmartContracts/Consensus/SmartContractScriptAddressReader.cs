using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    public sealed class SmartContractScriptAddressReader : IScriptAddressReader
    {
        private readonly IScriptAddressReader baseAddressReader;
        private readonly ICallDataSerializer callDataSerializer;

        public SmartContractScriptAddressReader(
            IScriptAddressReader addressReader,
            ICallDataSerializer callDataSerializer)
        {
            this.baseAddressReader = addressReader;
            this.callDataSerializer = callDataSerializer;
        }

        public string GetAddressFromScriptPubKey(Network network, Script script)
        {
            if (script.IsSmartContractCreate() || script.IsSmartContractCall())
            {
                var result = this.callDataSerializer.Deserialize(script.ToBytes());               
                return result.Value.ContractAddress?.ToAddress(network);
            }

            return this.baseAddressReader.GetAddressFromScriptPubKey(network, script);
        }
    }
}