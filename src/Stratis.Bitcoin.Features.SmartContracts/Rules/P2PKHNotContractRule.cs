using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// Used to check that people don't try and send funds to contracts via P2PKH.
    /// </summary>
    public class P2PKHNotContractRule : FullValidationConsensusRule, ISmartContractMempoolRule
    {
        private readonly IStateRepositoryRoot stateRepositoryRoot;

        public P2PKHNotContractRule(IStateRepositoryRoot stateRepositoryRoot)
        {
            this.stateRepositoryRoot = stateRepositoryRoot;
        }

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            foreach (Transaction transaction in block.Transactions)
            {
                this.CheckTransaction(transaction);
            }

            return Task.CompletedTask;
        }

        public void CheckTransaction(MempoolValidationContext context)
        {
            this.CheckTransaction(context.Transaction);
        }

        private void CheckTransaction(Transaction transaction)
        {
            foreach(TxOut output in transaction.Outputs)
            {
                if (PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                {
                    KeyId p2pkhParams = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(output.ScriptPubKey);
                    uint160 to = new uint160(p2pkhParams.ToBytes());
                    if (this.stateRepositoryRoot.GetAccountState(to) != null)
                        new ConsensusError("p2pkh-to-contract", "attempted send directly to contract address. use OP_CALL instead.").Throw();
                }
            }
        }
    }
}
