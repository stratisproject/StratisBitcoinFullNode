using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// This class and the base <see cref="ScriptDestinationReader"/> offers the ability to selectively replace <see cref="ScriptAddressReader"/>
    /// which can only parse a single address from a ScriptPubKey. ColdStaking scripts contain two addresses / pub key hashes.
    /// </summary>
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
