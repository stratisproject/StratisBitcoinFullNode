﻿using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.SmartContracts.Core;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    /// <summary>
    /// If a transaction's inputs contain an OP_SPEND opcode in the scriptsig, check that the transaction
    /// that occurs directly before contains OP_CREATE or OP_CALL in its outputs. Ensures that only a
    /// contract execution transaction is able to create OP_SPEND inputs.
    /// </summary>
    public class OpSpendRule : FullValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            for (var i = 0; i < block.Transactions.Count; i++)
            {
                var transaction = block.Transactions[i];

                // If the inputs to the transaction do not contain an OP_SPEND, continue.
                if (!transaction.IsSmartContractSpendTransaction())
                    continue;

                // If i == 0, there can be no previous OP_CALL or OP_CREATE, so OP_SPEND is invalid.
                if (i == 0)
                {
                    new ConsensusError("opspend-in-first-transaction", "the first transaction in the block contained an op-spend").Throw();
                };

                Transaction previousTransaction = block.Transactions[i - 1];

                // Check for OP_CREATE and OP_CALL because both opcodes can be followed by OP_SPEND.
                var previousWasOpCall = previousTransaction.Outputs.Any(o => o.ScriptPubKey.IsSmartContractExec());

                if (!previousWasOpCall)
                {
                    new ConsensusError("opspend-did-not-follow-opcall-or-opcreate", "transaction contained an op-spend that did not follow an op-call or an op-create").Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}