using System.Linq;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// This class is used to identify a <see cref="HdAddress" /> given a <see cref="Script" /> (scriptPubKey).
    /// </summary>
    /// <remarks>
    /// Being able to map scripts to addresses allows the wallet to identify the corresponding transactions (see
    /// <see cref="HdAddress.Transactions" />) that pay to those addresses regardless of the type of script being used.
    /// The methods are virtual so that the functionality can be overridden as required to support additional script types.
    /// </remarks>
    public class ScriptToAddressLookup
    {
        /// <summary>A collection of <see cref="HdAddress"/> objects eached keyed by a <see cref="Script"/> object.</summary>
        protected Dictionary<Script, HdAddress> keysLookup;

        /// <summary>
        /// Constructs this object.
        /// </summary>
        public ScriptToAddressLookup()
        {
            this.keysLookup = new Dictionary<Script, HdAddress>();
        }

        /// <summary>
        /// Returns the <see cref="HdAddress"/> values from the <see cref="ScriptToAddressLookup.keysLookup"/> collection.
        /// </summary>
        public virtual IEnumerable<HdAddress> Values => this.keysLookup.Values.AsEnumerable();

        /// <summary>
        /// Maps scripts to addresses.
        /// </summary>
        /// <param name="script">The script to map.</param>
        /// <param name="address">The address mapped to the script.</param>
        /// <returns>Return <c>true</c> if a mapping could be found and <c>false</c> otherwise.</returns>
        public virtual bool TryGetValue(Script script, out HdAddress address)
        {
            return this.keysLookup.TryGetValue(script, out address);
        }

        /// <summary>
        /// Returns the number of elements in the collection.
        /// </summary>
        public int Count => this.keysLookup.Count;

        /// <summary>
        /// The 'get' method returns the address that has been mapped to the script.
        /// The 'set' method records the address against the script as key.
        /// </summary>
        /// <param name="script">The script that acts as key.</param>
        /// <returns>The <see cref="HdAddress"/> object.</returns>
        public virtual HdAddress this[Script script]
        {
            get => this.keysLookup[script];
            set => this.keysLookup[script] = value;
        }
    }
}