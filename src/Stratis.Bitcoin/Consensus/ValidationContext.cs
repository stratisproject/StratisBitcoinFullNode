using System;
using System.Net;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// A context that is used by consensus that is required for validation, in case validation failed the <see cref="ValidationContext.Error"/> property will be set.
    /// It is used when a new block is downloaded or mined.
    /// </summary>
    public class ValidationContext
    {
        /// <summary>A value indicating the peer should not be banned.</summary>
        public const int BanDurationNoBan = -1;

        /// <summary>A value indicating the peer ban time should be <see cref="ConnectionManagerSettings.BanTimeSeconds"/>.</summary>
        public const int BanDurationDefaultBan = 0;

        /// <summary>The chain of headers associated with the block.</summary>
        public ChainedHeader ChainedHeader { get; set; }

        /// <summary>Downloaded or mined block to be validated.</summary>
        public Block Block { get; set; }

        /// <summary>
        /// The peer this block came from, <c>null</c> if the block was mined.
        /// </summary>
        public IPEndPoint Peer { get; set; }

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
        /// <remarks>
        /// Setting this value to be <see cref="BanDurationNoBan"/> will prevent the peer from being banned.
        /// Setting this value to be <see cref="BanDurationDefaultBan"/> will default to <see cref="ConnectionManagerSettings.BanTimeSeconds"/>.
        /// </remarks>
        public int BanDurationSeconds { get; set; }

        /// <summary>The context of the validation processes.</summary>
        public RuleContext RuleContext { get; set; }
    }
}