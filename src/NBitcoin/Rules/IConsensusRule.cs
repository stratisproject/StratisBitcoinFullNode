namespace NBitcoin.Rules
{
    public interface IBaseConsensusRule
    {
    }

    public interface IHeaderValidationConsensusRule : IBaseConsensusRule
    {
    }

    public interface IIntegrityValidationConsensusRule : IBaseConsensusRule
    {
    }

    public interface IPartialValidationConsensusRule : IBaseConsensusRule
    {
    }

    public interface IFullValidationConsensusRule : IBaseConsensusRule
    {
    }
}