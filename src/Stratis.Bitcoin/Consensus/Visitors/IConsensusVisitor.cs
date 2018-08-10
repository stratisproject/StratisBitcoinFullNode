namespace Stratis.Bitcoin.Consensus.Visitors
{
    public interface IConsensusVisitor
    {
        ConsensusVisitorResult Visit(ConsensusManager consensusManager);
    }
}
