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

        public DiConsensusRuleEngine(ConsensusRuleEngine implementation)
        {
            this.implementation = implementation;
        }

        public void Dispose()
        {
            this.implementation.Dispose();
        }

        public void Initialize(ChainedHeader chainTip)
        {
            this.implementation.Initialize(chainTip);
        }

        public ConsensusRuleEngine Register()
        {
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

        public uint256 GetBlockHash()
        {
            return this.implementation.GetBlockHash();
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