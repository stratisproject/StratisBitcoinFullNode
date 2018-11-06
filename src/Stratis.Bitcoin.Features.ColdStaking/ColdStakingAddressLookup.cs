using System;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// This class extends the <see cref="ScriptToAddressLookup"/> base class by handling cold staking scripts.
    /// </summary>
    /// <remarks>
    /// When looking up addresses we ensure that both the public key hashes found in the cold staking script
    /// are matched to the <see cref="ScriptToAddressLookup.scriptToAddressLookup"/> collection.
    /// Similarly when recording addresses we first identify which of the public key hashes
    /// matches the address being recorded and only record the address against that key.
    /// </remarks>
    public class ColdStakingAddressLookup : ScriptToAddressLookup
    {
        private readonly Network network;

        public ColdStakingAddressLookup(Network network) : base()
        {
            this.network = network;
        }

        /// <inheritdoc/>
        /// <remarks>The public key hashes in the cold staking script are individually mapped to addresses so search using both.</remarks>
        public override bool TryGetValue(Script script, out HdAddress address)
        {
            if (base.TryGetValue(script, out address))
                return true;

            // Search the lookup using the scripts of both keys.
            if (ColdStakingScriptTemplate.Instance.ExtractScriptPubKeyParameters(script, out KeyId hotPubKey, out KeyId coldPubKey))
            {
                if (this.keysLookup.TryGetValue(hotPubKey.ScriptPubKey, out address))
                {
                    // Sanity check for a situation we can't handle with the current wallet manager.
                    if (this.keysLookup.TryGetValue(coldPubKey.ScriptPubKey, out _))
                        throw new Exception("Can't have cold wallet and hot wallet on the same instance.");

                    return true;
                }

                if (this.keysLookup.TryGetValue(coldPubKey.ScriptPubKey, out address))
                    return true;
            }

            return false;
        }

        /// <inheritdoc/>
        /// <remarks>The public key hashes in the cold staking script are individually mapped to addresses.</remarks>
        public override HdAddress this[Script script]
        {
            get
            {
                // Use our special TryGetValue that also works with cold staking scripts to get the value.
                if (this.TryGetValue(script, out HdAddress address))
                    return address;

                // This method should throw an exception if the value is not found.
                throw new Exception($"Could not resolve address from script '{script}' using '{nameof(ScriptToAddressLookup)}' collection.");
            }

            set
            {
                // First identify which of the public key hashes matches the address being recorded
                // and only record the address against that key.
                if (ColdStakingScriptTemplate.Instance.ExtractScriptPubKeyParameters(script, out KeyId hotPubKey, out KeyId coldPubKey))
                {
                    TxDestination destination = value.ScriptPubKey.GetDestination(this.network);

                    if (destination == hotPubKey)
                    {
                        this.keysLookup[hotPubKey.ScriptPubKey] = value;
                    }
                    else if (destination == coldPubKey)
                    {
                        this.keysLookup[coldPubKey.ScriptPubKey] = value;
                    }
                    else
                    {
                        // This method should throw an exception if there is no relationship between the script and the address.
                        throw new Exception("The address can't be matched to the script.");
                    }
                }
                else
                {
                    base[script] = value;
                }
            }
        }
    }
}
