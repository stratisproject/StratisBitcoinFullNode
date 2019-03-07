using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// Creates a ConsensusRuleEngine with the ability to use injected consensus rules.
    /// Accepts an <see cref="IRuleRegistration"/> which behaves as a rule factory whose dependencies are also injected by the DI container.
    /// </summary>
    public class DiConsensusRuleEngine : IConsensusRuleEngine
    {
        private readonly ConsensusRuleEngine implementation;
        private readonly IRuleRegistration ruleRegistration;

        public DiConsensusRuleEngine(ConsensusRuleEngine implementation, IRuleRegistration ruleRegistration)
        {
            this.implementation = implementation;
            this.ruleRegistration = ruleRegistration;
        }

        public void Dispose()
        {
            this.implementation.Dispose();
        }

        public Task InitializeAsync(ChainedHeader chainTip)
        {
            return this.implementation.InitializeAsync(chainTip);
        }

        public ConsensusRuleEngine Register()
        {
            // Hack the rules onto Consensus
            this.ruleRegistration.RegisterRules(this.implementation.Network.Consensus);

            // Call the implementation to register the rules on the Network.
            this.implementation.Register();

            return this.implementation;
        }

        public T GetRule<T>() where T : ConsensusRuleBase
        {
            return this.implementation.GetRule<T>();
        }

        public RuleContext CreateRuleContext(ValidationContext validationContext)
        {
            return this.implementation.CreateRuleContext(validationContext);
        }

        public Task<uint256> GetBlockHashAsync()
        {
            return this.implementation.GetBlockHashAsync();
        }

        public Task<RewindState> RewindAsync()
        {
            return this.implementation.RewindAsync();
        }

        public ValidationContext HeaderValidation(ChainedHeader header)
        {
            return this.implementation.HeaderValidation(header);
        }

        public ValidationContext IntegrityValidation(ChainedHeader header, Block block)
        {
            return this.implementation.IntegrityValidation(header, block);
        }

        public Task<ValidationContext> PartialValidationAsync(ChainedHeader header, Block block)
        {
            return this.implementation.PartialValidationAsync(header, block);
        }

        public Task<ValidationContext> FullValidationAsync(ChainedHeader header, Block block)
        {
            return this.implementation.FullValidationAsync(header, block);
        }
    }
}