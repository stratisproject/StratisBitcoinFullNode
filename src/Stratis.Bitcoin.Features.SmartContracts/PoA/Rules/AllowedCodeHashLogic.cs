using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA.Rules
{
    /// <summary>
    /// Validates that the has of the supplied smart contract code is contained in a list of supplied hashes.
    /// </summary>
    public class AllowedCodeHashLogic : IContractTransactionValidationLogic
    {
        public void CheckContractTransaction(ContractTxData txData, Money suppliedBudget)
        {
            throw new System.NotImplementedException();
        }
    }
}