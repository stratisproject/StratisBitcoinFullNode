namespace NBitcoin.Rules
{
    public interface IConsensusRuleBase
    {
    }

    public interface IHeaderValidationConsensusRule : IConsensusRuleBase
    {
    }

    public interface IIntegrityValidationConsensusRule : IConsensusRuleBase
    {
    }

    public interface IPartialValidationConsensusRule : IConsensusRuleBase
    {
    }

    public interface IFullValidationConsensusRule : IConsensusRuleBase
    {
    }
}