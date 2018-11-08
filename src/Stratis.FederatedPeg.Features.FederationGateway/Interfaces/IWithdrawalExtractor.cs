using System.Collections.Generic;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// This component is responsible for finding all deposits made from the federation's
    /// multisig address to a target address, find out if they represent a cross chain transfer
    /// and if so, extract the details into an <see cref="IWithdrawal"/>.
    /// </summary>
    public interface IWithdrawalExtractor
    {
        IReadOnlyList<IWithdrawal> ExtractWithdrawalsFromBlock(Block block, int blockHeight);
    }
}