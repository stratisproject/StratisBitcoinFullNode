using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.CLR;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// Holds logic for validating a specific property of smart contract transactions.
    /// </summary>
    public interface IContractTransactionValidationLogic
    {
        /// <summary>
        /// Ensures that a transaction is valid, throwing a <see cref="ConsensusError"/> otherwise.
        /// </summary>
        /// <param name="txData">The included transaction data.</param>
        /// <param name="suppliedBudget">The total amount sent as the protocol fee.</param>
        void CheckContractTransaction(ContractTxData txData, Money suppliedBudget);
    }
}
