using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;
using TracerAttributes;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// This component is responsible for finding all transactions sent from the federation's
    /// multisig address to a target address, find out if they represent a cross chain transfer
    /// and if so, extract the details into an <see cref="IWithdrawal"/>.
    /// </summary>
    public interface IWithdrawalExtractor
    {
        IReadOnlyList<IWithdrawal> ExtractWithdrawalsFromBlock(Block block, int blockHeight);

        IWithdrawal ExtractWithdrawalFromTransaction(Transaction transaction, uint256 blockHash, int blockHeight);
    }

    [NoTrace]
    public class WithdrawalExtractor : IWithdrawalExtractor
    {
        /// <summary>
        /// Withdrawals have a particular format we look for.
        /// They will have 2 outputs when there is no change to be sent.
        /// </summary>
        private const int ExpectedNumberOfOutputsNoChange = 2;

        /// <summary>
        /// Withdrawals will have 3 outputs when there is change to be sent.
        /// </summary>
        private const int ExpectedNumberOfOutputsChange = 3;

        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly Network network;

        private readonly ILogger logger;

        private readonly BitcoinAddress multisigAddress;

        public WithdrawalExtractor(
            ILoggerFactory loggerFactory,
            IFederatedPegSettings federatedPegSettings,
            IOpReturnDataReader opReturnDataReader,
            Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.multisigAddress = federatedPegSettings.MultiSigAddress;
            this.opReturnDataReader = opReturnDataReader;
            this.network = network;
        }

        /// <inheritdoc />
        public IReadOnlyList<IWithdrawal> ExtractWithdrawalsFromBlock(Block block, int blockHeight)
        {
            var withdrawals = new List<IWithdrawal>();

            if (block.Transactions.Count <= 1)
                return withdrawals;

            foreach (Transaction transaction in block.Transactions)
            {
                IWithdrawal withdrawal = this.ExtractWithdrawalFromTransaction(transaction, block.GetHash(), blockHeight);

                if (withdrawal != null)
                    withdrawals.Add(withdrawal);
            }

            return withdrawals;
        }

        public IWithdrawal ExtractWithdrawalFromTransaction(Transaction transaction, uint256 blockHash, int blockHeight)
        {
            // Coinbase can't contain withdrawals.
            if (transaction.IsCoinBase)
                return null;

            // Withdrawal has a specific structure.
            if (transaction.Outputs.Count != ExpectedNumberOfOutputsNoChange
                && transaction.Outputs.Count != ExpectedNumberOfOutputsChange)
                return null;

            if (!this.IsOnlyFromMultisig(transaction))
                return null;

            if (!this.opReturnDataReader.TryGetTransactionId(transaction, out string depositId))
                return null;

            TxOut targetAddressOutput = transaction.Outputs.SingleOrDefault(this.IsTargetAddressCandidate);
            if (targetAddressOutput == null)
                return null;

            var withdrawal = new Withdrawal(
                uint256.Parse(depositId),
                transaction.GetHash(),
                targetAddressOutput.Value,
                targetAddressOutput.ScriptPubKey.GetDestinationAddress(this.network).ToString(),
                blockHeight,
                blockHash);

            return withdrawal;
        }

        /// <summary>
        /// Discerns whether an output is a transfer to a destination other than the federation multisig.
        /// </summary>
        private bool IsTargetAddressCandidate(TxOut output)
        {
            return output.ScriptPubKey != this.multisigAddress.ScriptPubKey && !output.ScriptPubKey.IsUnspendable;
        }

        /// <summary>
        /// Identify whether a transaction's inputs are coming only from the federation multisig.
        /// </summary>
        /// <param name="transaction">The transaction to check.</param>
        /// <returns>True if all inputs are from the federation multisig.</returns>
        private bool IsOnlyFromMultisig(Transaction transaction)
        {
            if (!transaction.Inputs.Any())
                return false;

            return transaction.Inputs.All(i => i.ScriptSig?.GetSignerAddress(this.network) == this.multisigAddress);
        }
    }
}