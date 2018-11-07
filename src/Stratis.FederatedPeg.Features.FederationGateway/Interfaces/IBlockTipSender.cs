using System.Threading.Tasks;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IBlockTipSender
    {
        Task SendBlockTipAsync(IBlockTip blockTip);
    }
}