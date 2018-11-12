using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// This component is responsible for finding all deposits made to a given address
    /// in a given block, find out if they should trigger a cross chain transfer, and if so
    /// extracting the transfers' details.
    /// </summary>
    public interface IDepositExtractor
    {
        IReadOnlyList<IDeposit> ExtractDepositsFromBlock(Block block, int blockHeight);
      
        IDeposit ExtractDepositFromTransaction(Transaction transaction, int blockHeight, uint256 blockHash);

        IMaturedBlockDeposits ExtractMaturedBlockDeposits(ChainedHeader chainedHeader);

        uint MinimumDepositConfirmations { get; }
    }
}
