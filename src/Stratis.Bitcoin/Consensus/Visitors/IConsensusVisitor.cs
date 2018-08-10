using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    public interface IConsensusVisitor<T>
    {
        Task<T> VisitAsync(ConsensusManager consensusManager);
    }
}
