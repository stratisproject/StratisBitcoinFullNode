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
            foreach (var transaction in block.Transactions)
            {
                var depositsToMultisig = transaction.Outputs.Where(output =>
                    output.ScriptPubKey == this.depositScript
                    && !output.IsDust(FeeRate.Zero)).ToList();

                if (!depositsToMultisig.Any()) continue;

                var targetAddress = this.opReturnDataReader.TryGetTargetAddressFromOpReturn(transaction);
                if (string.IsNullOrWhiteSpace(targetAddress)) continue;

                this.logger.LogInformation("Processing received transaction with address: {0}. Transaction hash: {1}.",
                    targetAddress, transaction.GetHash());

                var deposit = new Deposit(transaction.GetHash(), depositsToMultisig.Sum(o => o.Value), targetAddress, blockHeight, block.GetHash());
                deposits.Add(deposit);
            }

            return deposits.AsReadOnly();
        }
    }
}