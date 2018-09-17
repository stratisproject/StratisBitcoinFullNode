using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Networks.ProtocolVersion
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
    public class ProtocolVersionBase : Enumeration, IProtocolVersion
    {
        /// <summary>
        /// Protocol version.
        /// </summary>
        public static readonly IProtocolVersion Protocol = new ProtocolVersionBase(70012, nameof(ProtocolVersionBase).ToLowerInvariant());

        /// <summary>
        /// Alternate coin protocol version.
        /// </summary>
        public static readonly IProtocolVersion AltProtocal = new ProtocolVersionBase(70000, nameof(AltProtocal).ToLowerInvariant());

        /// <summary>
        /// Initial protocol version, to be increased after version/verack negotiation.
        /// </summary>
        public static readonly IProtocolVersion InitProtocol = new ProtocolVersionBase(209, nameof(InitProtocol).ToLowerInvariant());

        /// <summary>
        /// Disconnect from peers older than this protocol version.
        /// </summary>
        public static readonly IProtocolVersion MinPeers = new ProtocolVersionBase(209, nameof(MinPeers).ToLowerInvariant());

        /// <summary>
        /// nTime field added to CAddress, starting with this version;
        /// if possible, avoid requesting addresses nodes older than this.
        /// </summary>
        public static readonly IProtocolVersion CAddressTime = new ProtocolVersionBase(31402, nameof(CAddressTime).ToLowerInvariant());

        /// <summary>
        /// Only request blocks from nodes outside this range of versions (START).
        /// </summary>
        public static readonly IProtocolVersion NoBlocksStart = new ProtocolVersionBase(32000, nameof(NoBlocksStart).ToLowerInvariant());

        /// <summary>
        /// Only request blocks from nodes outside this range of versions (END).
        /// </summary>
        public static readonly IProtocolVersion NoBlocksEnd = new ProtocolVersionBase(32400, nameof(NoBlocksEnd).ToLowerInvariant());

        /// <summary>
        /// BIP 0031, pong message, is enabled for all versions AFTER this one.
        /// </summary>
        public static readonly IProtocolVersion Bip31 = new ProtocolVersionBase(60000, nameof(Bip31).ToLowerInvariant());

        /// <summary>
        /// "mempool" command, enhanced "getdata" behaviour starts with this version.
        /// </summary>
        public static readonly IProtocolVersion MempoolGetData = new ProtocolVersionBase(60002, nameof(MempoolGetData).ToLowerInvariant());

        /// <summary>
        /// "reject" command.
        /// </summary>
        public static readonly IProtocolVersion Reject = new ProtocolVersionBase(70002, nameof(Reject).ToLowerInvariant());

        /// <summary>
        /// ! "filter*" commands are disabled without NODE_BLOOM after and including this version.
        /// </summary>
        public static readonly IProtocolVersion NoBloom = new ProtocolVersionBase(70011, nameof(NoBloom).ToLowerInvariant());

        /// <summary>
        /// ! "sendheaders" command and announcing blocks with headers starts with this version.
        /// </summary>
        public static readonly IProtocolVersion SendHeaders = new ProtocolVersionBase(70012, nameof(SendHeaders).ToLowerInvariant());

        /// <summary>
        /// ! Version after which witness support potentially exists.
        /// </summary>
        public static readonly IProtocolVersion Witness = new ProtocolVersionBase(70012, nameof(Witness).ToLowerInvariant());

        public ProtocolVersionBase() {}

        protected ProtocolVersionBase(int id, string name) : base(id, name) {}

        /// <summary>
        /// List all versions 
        /// </summary>
        public static IEnumerable<IProtocolVersion> GetAll<T>() where T : Enumeration, IProtocolVersion, new()
        {
            IEnumerable<IProtocolVersion> list = Enumeration.GetAll<T, IProtocolVersion>();
            return list;
        }
    }
}
