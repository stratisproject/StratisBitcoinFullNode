using System.Collections.Generic;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlockDeposits
    {
        IReadOnlyList<IDeposit> Deposits { get; set; }

        IMaturedBlock Block { get; set; }
    }
}