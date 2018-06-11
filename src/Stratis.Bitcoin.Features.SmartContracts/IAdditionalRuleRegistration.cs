using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public interface IAdditionalRuleRegistration : IRuleRegistration
    {
        void SetPreviousRegistration(IRuleRegistration previousRegistration);
    }
}