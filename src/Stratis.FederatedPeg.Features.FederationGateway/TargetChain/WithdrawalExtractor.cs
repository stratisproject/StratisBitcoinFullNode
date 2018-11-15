using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Subjects;

using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class WithdrawalExtractor : IWithdrawalExtractor
    {
        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly Network network;

        private readonly ILogger logger;

        private readonly BitcoinAddress multisigAddress;

        private readonly IWithdrawalReceiver withdrawalReceiver;

        public WithdrawalExtractor(
            ILoggerFactory loggerFactory,
            IFederationGatewaySettings federationGatewaySettings,
            IOpReturnDataReader opReturnDataReader,
            IWithdrawalReceiver withdrawalReceiver,
            Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.multisigAddress = federationGatewaySettings.MultiSigAddress;
            this.opReturnDataReader = opReturnDataReader;
            this.network = network;

            this.withdrawalReceiver = withdrawalReceiver;
        }

        /// <inheritdoc />
        public IReadOnlyList<IWithdrawal> ExtractWithdrawalsFromBlock(Block block, int blockHeight)
        {
            var withdrawals = new List<IWithdrawal>();
            foreach (var transaction in block.Transactions)
            {
                var withdrawal = ExtractWithdrawalFromTransaction(transaction, block.GetHash(), blockHeight);
                if (withdrawal != null) withdrawals.Add(withdrawal);
            }

            var withdrawalsFromBlock = withdrawals.AsReadOnly();

            this.withdrawalReceiver.ReceiveWithdrawals(withdrawalsFromBlock);

            return withdrawalsFromBlock;
        }

        private IWithdrawal ExtractWithdrawalFromTransaction(Transaction transaction, uint256 blockHash, int blockHeight)
        {
            if (transaction.Outputs.Count(this.IsTargetAddressCandidate) != 1) return null;
            if (!IsOnlyFromMultisig(transaction)) return null;

            var depositId = this.opReturnDataReader.TryGetTransactionId(transaction);
            if (string.IsNullOrWhiteSpace(depositId)) return null;

            this.logger.LogInformation(
                "Processing received transaction with source deposit id: {0}. Transaction hash: {1}.",
                depositId,
                transaction.GetHash());

            var targetAddressOutput = transaction.Outputs.Single(this.IsTargetAddressCandidate);
            var withdrawal = new Withdrawal(
                uint256.Parse(depositId),
                transaction.GetHash(),
                targetAddressOutput.Value,
                targetAddressOutput.ScriptPubKey.GetScriptAddress(this.network).ToString(),
                blockHeight,
                blockHash);
            return withdrawal;
        }

        private bool IsTargetAddressCandidate(TxOut output)
        {
            return output.ScriptPubKey.Hash.GetAddress(this.network) != this.multisigAddress && !output.ScriptPubKey.IsUnspendable;
        }

        private bool IsOnlyFromMultisig(Transaction transaction)
        {
            if (!transaction.Inputs.Any()) return false;
            return transaction.Inputs.All(
                    i => i.ScriptSig?.GetSignerAddress(this.network) == this.multisigAddress);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.withdrawalReceiver?.Dispose();
        }
    }
}