using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// This interface defines a DNS server used by the StratisDnsD daemon to support a DNS Seed service.
    /// </summary>
    public interface IDnsServer
    {
        /// <summary>
        /// Gets the current <see cref="IMasterFile"/> instance associated with the <see cref="IDnsServer"/>.
        /// </summary>
        IMasterFile MasterFile { get; }

        /// <summary>
        /// Gets the metrics for the DNS server.
        /// </summary>
        DnsMetric Metrics { get; }

        /// <summary>
        /// Initializes the DNS Server.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Starts listening for DNS requests.
        /// </summary>
        /// <param name="dnsListenPort">The port to listen on.</param>
        /// <param name="token">The token used to cancel the listen.</param>
        /// <returns>A task used to await the listen operation.</returns>
        Task ListenAsync(int dnsListenPort, CancellationToken token);

        /// <summary>
        /// Swaps in a new version of the cached DNS masterfile used by the DNS server.
        /// </summary>
        /// <remarks>
        /// The <see cref="DnsFeature"/> object is designed to produce a whitelist of peers from the <see cref="P2P.IPeerAddressManager"/>
        /// object which is then periodically formed into a new masterfile instance and applied to the <see cref="IDnsServer"/> object.  The
        /// masterfile is swapped for efficiency, rather than applying a merge operation to the existing masterfile, or clearing the existing
        /// masterfile and re-adding the peer entries (which could cause some interim DNS resolve requests to fail).
        /// </remarks>
        /// <param name="masterFile">The new masterfile to swap in.</param>
        void SwapMasterfile(IMasterFile masterFile);
    }
}