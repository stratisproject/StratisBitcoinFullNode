namespace NBitcoin.Rules
{
    public interface IConsensusRule
    {
    }

    public interface ISyncConsensusRule : IConsensusRule
    {
    }

    public interface IAsyncConsensusRule : IConsensusRule
    {
    }
}