using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly Script withdrawalScript;

        public WithdrawalExtractor(
            ILoggerFactory loggerFactory,
            IFederationGatewaySettings federationGatewaySettings,
            IOpReturnDataReader opReturnDataReader,
            Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.withdrawalScript = federationGatewaySettings.MultiSigRedeemScript;
            this.opReturnDataReader = opReturnDataReader;
            this.network = network;
        }

        /// <inheritdoc />
        public IReadOnlyList<IWithdrawal> ExtractWithdrawalsFromBlock(Block block, int blockHeight)
        {
            var withdrawals = new List<IWithdrawal>();
            foreach (var transaction in block.Transactions)
            {
                if (transaction.Outputs.Count(this.IsTargetAddressCandidate) != 1) continue;

                var withdrawalFromMultisig = transaction.Inputs.Where(input =>
                    input.ScriptSig == this.withdrawalScript).ToList();

                if (!withdrawalFromMultisig.Any()) continue;

                var depositId = this.opReturnDataReader.TryGetTransactionId(transaction);
                if (string.IsNullOrWhiteSpace(depositId)) continue;

                this.logger.LogInformation("Processing received transaction with source deposit id: {0}. Transaction hash: {1}.",
                    depositId, transaction.GetHash());

                var targetAddressOutput = transaction.Outputs.Single(this.IsTargetAddressCandidate);
                var withdrawal = new Withdrawal(uint256.Parse(depositId), transaction.GetHash(), targetAddressOutput.Value,
                    targetAddressOutput.ScriptPubKey.GetScriptAddress(this.network).ToString(), blockHeight, block.GetHash());
                withdrawals.Add(withdrawal);
            }

            return withdrawals.AsReadOnly();
        }

        private bool IsTargetAddressCandidate(TxOut output)
        {
            return output.ScriptPubKey != this.withdrawalScript && !output.ScriptPubKey.IsUnspendable;
        }
    }
}