using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    public interface IConsensusVisitor
    {
        Task<ConsensusVisitorResult> VisitAsync(ConsensusManager consensusManager);
    }
}
