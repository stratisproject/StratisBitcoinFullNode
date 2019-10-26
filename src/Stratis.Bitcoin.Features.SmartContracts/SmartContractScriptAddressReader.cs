using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    /// <summary>
    /// Smart contract specific logic to get the contract address from the <see cref="ContractTxData"/>.
    /// </summary>
    public sealed class SmartContractScriptAddressReader : IScriptAddressReader
    {
        private readonly IScriptAddressReader baseAddressReader;
        private readonly ICallDataSerializer callDataSerializer;

        public SmartContractScriptAddressReader(
            ScriptAddressReader addressReader,
            ICallDataSerializer callDataSerializer)
        {
            this.baseAddressReader = addressReader;
            this.callDataSerializer = callDataSerializer;
        }

        public string GetAddressFromScriptPubKey(Network network, Script script)
        {
            if (script.IsSmartContractCreate() || script.IsSmartContractCall())
            {
                Result<ContractTxData> result = this.callDataSerializer.Deserialize(script.ToBytes());
                return result.Value.ContractAddress?.ToAddress().ToString();
            }

            return this.baseAddressReader.GetAddressFromScriptPubKey(network, script);
        }
    }
}