using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// This component is responsible for finding all deposits made from the federation's
    /// multisig address to a target address, find out if they represent a cross chain transfer
    /// and if so, extract the details into an <see cref="IWithdrawal"/>.
    /// </summary>
    public interface IWithdrawalExtractor
    {
        IReadOnlyList<IWithdrawal> ExtractWithdrawalsFromBlock(Block block, int blockHeight);

        IWithdrawal ExtractWithdrawalFromTransaction(Transaction transaction, uint256 blockHash, int blockHeight);
    }

    public class WithdrawalExtractor : IWithdrawalExtractor
    {
        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly Network network;

        private readonly ILogger logger;

        private readonly BitcoinAddress multisigAddress;

        public WithdrawalExtractor(
            ILoggerFactory loggerFactory,
            IFederationGatewaySettings federationGatewaySettings,
            IOpReturnDataReader opReturnDataReader,
            Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.multisigAddress = federationGatewaySettings.MultiSigAddress;
            this.opReturnDataReader = opReturnDataReader;
            this.network = network;
        }

        /// <inheritdoc />
        public IReadOnlyList<IWithdrawal> ExtractWithdrawalsFromBlock(Block block, int blockHeight)
        {
            var withdrawals = new List<IWithdrawal>();
            foreach (Transaction transaction in block.Transactions)
            {
                IWithdrawal withdrawal = this.ExtractWithdrawalFromTransaction(transaction, block.GetHash(), blockHeight);
                if (withdrawal != null) withdrawals.Add(withdrawal);
            }

            ReadOnlyCollection<IWithdrawal> withdrawalsFromBlock = withdrawals.AsReadOnly();

            return withdrawalsFromBlock;
        }

        public IWithdrawal ExtractWithdrawalFromTransaction(Transaction transaction, uint256 blockHash, int blockHeight)
        {
            if (transaction.Outputs.Count(this.IsTargetAddressCandidate) != 1) return null;
            if (!this.IsOnlyFromMultisig(transaction)) return null;

            if (!this.opReturnDataReader.TryGetTransactionId(transaction, out string depositId))
                return null;

            this.logger.LogDebug(
                "Processing received transaction with source deposit id: {0}. Transaction hash: {1}.",
                depositId,
                transaction.GetHash());

            TxOut targetAddressOutput = transaction.Outputs.Single(this.IsTargetAddressCandidate);
            var withdrawal = new Withdrawal(
                uint256.Parse(depositId),
                transaction.GetHash(),
                targetAddressOutput.Value,
                targetAddressOutput.ScriptPubKey.GetDestinationAddress(this.network).ToString(),
                blockHeight,
                blockHash);

            return withdrawal;
        }

        private bool IsTargetAddressCandidate(TxOut output)
        {
            return output.ScriptPubKey != this.multisigAddress.ScriptPubKey && !output.ScriptPubKey.IsUnspendable;
        }

        private bool IsOnlyFromMultisig(Transaction transaction)
        {
            if (!transaction.Inputs.Any()) return false;
            return transaction.Inputs.All(
                    i => i.ScriptSig?.GetSignerAddress(this.network) == this.multisigAddress);
        }
    }
}