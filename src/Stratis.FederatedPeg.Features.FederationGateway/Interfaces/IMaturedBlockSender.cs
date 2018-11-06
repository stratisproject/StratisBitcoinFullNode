using System;
using System.Threading.Tasks;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlockSender
    {
        Task SendMaturedBlockDepositsAsync(IMaturedBlockDeposits maturedBlockDeposits);
    }
}