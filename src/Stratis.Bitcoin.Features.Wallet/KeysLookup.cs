using System.Linq;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// This class encapsulates the keysLookup dictionary for use in the <see cref="WalletManager"/>.
    /// The methods are virtual so that the functionality can be overridden as required.
    /// </summary>
    public class KeysLookup
    {
        protected Dictionary<Script, HdAddress> keysLookup;

        /// <summary>
        /// Constructs this object.
        /// </summary>
        public KeysLookup()
        {
            this.keysLookup = new Dictionary<Script, HdAddress>();
        }

        /// <summary>
        /// Returns the <see cref="HdAddress"/> values from the <see cref="KeysLookup.keysLookup"/> collection.
        /// </summary>
        public virtual IEnumerable<HdAddress> Values
        {
            get { return this.keysLookup.Values.AsEnumerable(); }
        }

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
            get { return this.keysLookup[script]; }
            set { this.keysLookup[script] = value; }
        }
    }
}

