using System.Collections.Generic;
using System.Net;
using NBitcoin;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>
    /// Configuration settings used to initialize a FederationGateway.
    /// </summary>
    public interface IFederationGatewaySettings
    {
        /// <summary>
        /// Indicates whether this is the main chain. Set if the "-mainchain" switch was used.
        /// </summary>
        bool IsMainChain { get; }

        /// <summary>
        /// Ip Endpoints for the other nodes in the federation.
        /// </summary>
        IEnumerable<IPEndPoint> FederationNodeIpEndPoints { get; }

        /// <summary>
        /// Public keys of other federation members.
        /// </summary>
        PubKey[] FederationPublicKeys { get; }

        /// <summary>
        /// Public key of this federation member.
        /// </summary>
        string PublicKey { get; }

        /// <summary>
        /// The API port used to communicate with node on the counter chain.
        /// </summary>
        int CounterChainApiPort { get; }

        /// <summary>
        /// For the M of N multisig, this is the number of signers required to reach a quorum.
        /// </summary>
        int MultiSigM { get; }

        /// <summary>
        /// For the M of N multisig, this is the number of members in the federation.
        /// </summary>
        int MultiSigN { get; }

        /// <summary>
        /// The mimimum confirmations required for coins added to withdrawals.
        /// </summary>
        int MinCoinMaturity { get; }

        /// <summary>
        /// The transaction fee required for withdrawals.
        /// </summary>
        Money TransactionFee { get; }

        /// <summary>
        /// Address for the MultiSig script.
        /// </summary>
        BitcoinAddress MultiSigAddress { get; }

        /// <summary>
        /// Pay2Multisig redeem script.
        /// </summary>
        Script MultiSigRedeemScript { get; }

        /// <summary>
        /// The amount of blocks under which multisig deposit transactions need to be buried before the cross chains transfer actually trigger.
        /// </summary>
        uint MinimumDepositConfirmations { get; }
    }
}