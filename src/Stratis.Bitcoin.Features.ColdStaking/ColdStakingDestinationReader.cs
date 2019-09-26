using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    public class ColdStakingDestinationReader : ScriptDestinationReader, IScriptDestinationReader
    {
        public ColdStakingDestinationReader(ScriptAddressReader scriptAddressReader) : base(scriptAddressReader)
        {
        }

        public override IEnumerable<TxDestination> GetDestinationFromScriptPubKey(Network network, Script redeemScript)
        {
            if (ColdStakingScriptTemplate.Instance.ExtractScriptPubKeyParameters(redeemScript, out KeyId hotPubKeyHash, out KeyId coldPubKeyHash))
            {
                yield return hotPubKeyHash;
                yield return coldPubKeyHash;
            }
            else
            {
                base.GetDestinationFromScriptPubKey(network, redeemScript);
            }
        }
    }
}
