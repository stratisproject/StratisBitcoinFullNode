using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedPeg.Features.FederationGateway.Federation
{
    /// <summary>
    /// Represents an M-of-N federation where N is the number of members in the federation and M
    /// is the quorum that needs to be reached in order that a Transaction is valid.
    /// M-of-N eg 2-of-3.
    /// </summary>
    public interface IFederation
    {
        /// <summary>
        /// The number of members in the federation. 
        /// </summary>
        int N { get; }

        /// <summary>
        /// The number of members required to sign transaction (quorum). 
        /// </summary>
        int M { get; }

        /// <summary>
        /// A list of all the members with their public info.
        /// </summary>
        IReadOnlyList<FederationMember> Members { get; }

        /// <summary>
        /// Gets the public keys for the specified chain.
        /// </summary>
        /// <param name="chain"></param>
        /// <returns>An array of keys as strings.</returns>
        string[] GetPublicKeys(Chain chain);

        /// <summary>
        /// Creates the ScriptPubKey for the specified chain.
        /// </summary>
        /// <param name="chain">The chain (either sidechain or mainchain).</param>
        /// <returns>The ScriptPubKey (redeem script.)</returns>
        Script GenerateScriptPubkey(Chain chain);
    }

    // Todo: Strickly N is not required other than for the check in the ctor.  Consider removing.
    /// <inheritdoc/>
    public class Federation : IFederation
    {
        /// <inheritdoc/>
        public int N { get; }

        /// <inheritdoc/>
        public int M { get; }

        /// <inheritdoc/>
        public IReadOnlyList<FederationMember> Members { get; }

        /// <summary>
        /// Create an M-of-N federation. Throws <exception cref="ArgumentException"></exception> if
        /// n does not match the number of members specified.
        /// </summary>
        /// <param name="m">The number of members required to sign transaction (quorum).</param>
        /// <param name="n">The number of members in the federation.</param>
        /// <param name="members">Enumerable list of FederationMembers.</param>
        public Federation(int m, int n, IEnumerable<FederationMember> members)
        {
            if (members.Count() != n)
                throw new ArgumentException($"Expected {n} members but found {members.Count()} federation members.");

            this.N = n;
            this.M = m;

            this.Members = new List<FederationMember>(members);
        }

        ///<inheritdoc/>
        public Script GenerateScriptPubkey(Chain chain)
        {
            var pubKeys = new List<PubKey>();
            foreach (FederationMember member in this.Members)
                pubKeys.Add(chain == Chain.Mainchain ? member.PublicKeyMainChain : member.PublicKeySideChain);

            // The order needs to be consistent.
            var keys = (from p in pubKeys orderby p.ToHex() select p).ToArray();
            return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(this.M, keys);
        }

        /// <inheritdoc/>
        public string[] GetPublicKeys(Chain chain) =>
            chain == Chain.Mainchain
                ? (from a in this.Members select a.PublicKeyMainChain.ToString()).ToArray()
                : (from a in this.Members select a.PublicKeySideChain.ToString()).ToArray();
    }
}