using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class DepositExtractor : IDepositExtractor
    {
        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILogger logger;

        private readonly Script depositScript;

        public DepositExtractor(
            ILoggerFactory loggerFactory,
            IFederationGatewaySettings federationGatewaySettings,
            IOpReturnDataReader opReturnDataReader)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.depositScript = federationGatewaySettings.MultiSigRedeemScript;
            this.opReturnDataReader = opReturnDataReader;
        }

        /// <inheritdoc />
        public IReadOnlyList<IDeposit> ExtractDepositsFromBlock(Block block, int blockHeight)
        {
            var deposits = new List<IDeposit>();
            uint256 blockHash = block.GetHash();

            foreach (Transaction transaction in block.Transactions)
            {
                IDeposit deposit = ExtractDepositFromTransaction(transaction, blockHeight, blockHash);
                if (deposit != null)
                {
                    deposits.Add(deposit);
                }
            }

            return deposits.AsReadOnly();
        }

        /// <inheritdoc />
        public IDeposit ExtractDepositFromTransaction(Transaction transaction, int blockHeight, uint256 blockHash)
        {
            var depositsToMultisig = transaction.Outputs.Where(output =>
                output.ScriptPubKey == this.depositScript
                && !output.IsDust(FeeRate.Zero)).ToList();

            if (!depositsToMultisig.Any())
                return null;

            var targetAddress = this.opReturnDataReader.TryGetTargetAddress(transaction);
            if (string.IsNullOrWhiteSpace(targetAddress))
                return null;

            this.logger.LogInformation("Processing received transaction with address: {0}. Transaction hash: {1}.",
                targetAddress, transaction.GetHash());

            return new Deposit(transaction.GetHash(), depositsToMultisig.Sum(o => o.Value), targetAddress, blockHeight, blockHash);
        }
    }
}