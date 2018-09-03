using System;
using System.Net;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// A context that is used by consensus that is required for validation, in case validation failed the <see cref="ValidationContext.Error"/> property will be set.
    /// It is used when a new block is downloaded or mined.
    /// </summary>
    public class ValidationContext
    {
        /// <summary>Chained header of the block being validated.</summary>
        public ChainedHeader ChainedHeaderToValidate { get; set; }

        /// <summary>Downloaded or mined block to be validated.</summary>
        public Block BlockToValidate { get; set; }

        /// <summary>If the block validation failed this will be set with the reason of failure.</summary>
        public ConsensusError Error { get; set; }

        /// <summary>
        /// If the block validation failed with <see cref="ConsensusErrors.BlockTimestampTooFar"/>
        /// then this is set to a time until which the block should be marked as invalid. Otherwise it is <c>null</c>.
        /// </summary>
        public DateTime? RejectUntil { get; set; }

        /// <summary>
        /// If the block validation failed with a <see cref="ConsensusError"/> that is considered malicious the peer will get banned.
        /// The ban, unless specified otherwise, will default to <see cref="ConnectionManagerSettings.BanTimeSeconds"/>.
        /// </summary>
        public int BanDurationSeconds { get; set; }

        /// <summary>Services that are missing from the peers.</summary>
        /// <remarks>
        /// Set in case some information is missing from the block which leads
        /// to inability to validate the block properly. Set to <c>null</c> otherwise.
        /// </remarks>
        public NetworkPeerServices? MissingServices { get; set; }
    }
}