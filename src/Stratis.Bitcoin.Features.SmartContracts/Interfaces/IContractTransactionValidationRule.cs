using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    public interface IContractTransactionFullValidationRule : IContractTransactionValidationRule { }

    public interface IContractTransactionPartialValidationRule : IContractTransactionValidationRule { }

    /// <summary>
    /// Holds logic for validating a specific property of smart contract transactions.
    /// </summary>
    public interface IContractTransactionValidationRule
    {
        /// <summary>
        /// Ensures that a transaction is valid, throwing a <see cref="ConsensusError"/> otherwise.
        /// </summary>
        /// <param name="txData">The included transaction data.</param>
        /// <param name="suppliedBudget">The total amount sent as the protocol fee.</param>
        void CheckContractTransaction(ContractTxData txData, Money suppliedBudget);
    }
}
