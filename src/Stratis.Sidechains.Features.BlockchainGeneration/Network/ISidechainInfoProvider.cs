using System.Threading.Tasks;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Network
{
    public interface ISidechainInfoProvider
    {
        SidechainInfo GetSidechainInfo(string sidechainName);
        Task<SidechainInfo> GetSidechainInfoAsync(string sidechainName);
        void VerifyFolder(string filename);
    }
}
