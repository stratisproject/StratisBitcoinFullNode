using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Networks
{
    /// <summary>
    /// Class enumeration containing protocol versions, by name and version number (Id).
    /// </summary>
    /// <remarks>
    /// This class contains baseline versions.
    /// <para>
    /// Future versions will go into either Bitcoin <see cref="BitcoinProtocolVersion"/> or Stratis <see cref="StratisProtocolVersion"/>.
    /// </para>
    /// </remarks>
    public class ProtocolVersion : Enumeration
    {
        /// <summary>
        /// Protocol version.
        /// </summary>
        public static ProtocolVersion Protocol = new ProtocolVersion(70012, nameof(ProtocolVersion).ToLowerInvariant());

        /// <summary>
        /// Alternate coin protocol version.
        /// </summary>
        public static ProtocolVersion AltProtocal = new ProtocolVersion(70000, nameof(AltProtocal).ToLowerInvariant());

        /// <summary>
        /// Initial protocol version, to be increased after version/verack negotiation.
        /// </summary>
        public static ProtocolVersion InitProtocol = new ProtocolVersion(209, nameof(InitProtocol).ToLowerInvariant());

        /// <summary>
        /// Disconnect from peers older than this protocol version.
        /// </summary>
        public static ProtocolVersion MinPeers = new ProtocolVersion(209, nameof(MinPeers).ToLowerInvariant());

        /// <summary>
        /// nTime field added to CAddress, starting with this version;
        /// if possible, avoid requesting addresses nodes older than this.
        /// </summary>
        public static ProtocolVersion CAddressTime = new ProtocolVersion(31402, nameof(CAddressTime).ToLowerInvariant());

        /// <summary>
        /// Only request blocks from nodes outside this range of versions (START).
        /// </summary>
        public static ProtocolVersion NoBlocksStart = new ProtocolVersion(32000, nameof(NoBlocksStart).ToLowerInvariant());

        /// <summary>
        /// Only request blocks from nodes outside this range of versions (END).
        /// </summary>
        public static ProtocolVersion NoBlocksEnd = new ProtocolVersion(32400, nameof(NoBlocksEnd).ToLowerInvariant());

        /// <summary>
        /// BIP 0031, pong message, is enabled for all versions AFTER this one.
        /// </summary>
        public static ProtocolVersion Bip31 = new ProtocolVersion(60000, nameof(Bip31).ToLowerInvariant());

        /// <summary>
        /// "mempool" command, enhanced "getdata" behaviour starts with this version.
        /// </summary>
        public static ProtocolVersion MempoolGetData = new ProtocolVersion(60002, nameof(MempoolGetData).ToLowerInvariant());

        /// <summary>
        /// "reject" command.
        /// </summary>
        public static ProtocolVersion Reject = new ProtocolVersion(70002, nameof(Reject).ToLowerInvariant());

        /// <summary>
        /// ! "filter*" commands are disabled without NODE_BLOOM after and including this version.
        /// </summary>
        public static ProtocolVersion NoBloom = new ProtocolVersion(70011, nameof(NoBloom).ToLowerInvariant());

        /// <summary>
        /// ! "sendheaders" command and announcing blocks with headers starts with this version.
        /// </summary>
        public static ProtocolVersion SendHeaders = new ProtocolVersion(70012, nameof(SendHeaders).ToLowerInvariant());

        /// <summary>
        /// ! Version after which witness support potentially exists.
        /// </summary>
        public static ProtocolVersion Witness = new ProtocolVersion(70012, nameof(Witness).ToLowerInvariant());

        /// <summary>
        /// short-id-based block download starts with this version.
        /// </summary>
        public static ProtocolVersion ShortIdBlocks = new ProtocolVersion(70014, nameof(ShortIdBlocks).ToLowerInvariant());

        protected ProtocolVersion() {}

        protected ProtocolVersion(int id, string name) : base(id, name) {}

        /// <summary>
        /// List all baseline versions.
        /// </summary>
        public virtual IEnumerable<ProtocolVersion> List() =>
            new[] {
                Protocol,
                AltProtocal,
                InitProtocol,
                MinPeers,
                CAddressTime,
                NoBlocksStart,
                NoBlocksEnd,
                Bip31,
                MempoolGetData,
                Reject,
                NoBloom,
                SendHeaders,
                Witness,
                ShortIdBlocks
            };
    }
}
