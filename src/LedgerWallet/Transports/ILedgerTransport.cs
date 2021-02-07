using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
    public interface ILedgerTransport
    {
        Task<byte[][]> Exchange(byte[][] apdus, CancellationToken cancellation);
    }
}
