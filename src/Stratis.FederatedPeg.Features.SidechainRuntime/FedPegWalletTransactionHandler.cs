using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

//This is experimental while we are waiting for a generic OP_RETURN function in the full node wallet.

namespace Stratis.FederatedPeg.Features.SidechainRuntime
{
    public class FedPegWalletTransactionHandler : WalletTransactionHandler
    {
        private readonly ILogger logger;

        public FedPegWalletTransactionHandler(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletFeePolicy walletFeePolicy,
            Network network)
        :base(loggerFactory, walletManager, walletFeePolicy, network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        //TODO: Get this added to the parent BuildTransaction functionality instead of using an override.
        //TODO: This change also removes the need for a 'services.Replace(descriptor)' in the feature builder.
        public override Transaction BuildTransaction(TransactionBuildContext context)
        {
            this.InitializeTransactionBuilder(context);

            if (context.Shuffle)
                context.TransactionBuilder.Shuffle();
          
            if (context is FedPegTransactionBuildContext)
            {
                var fedPegTransactionBuildContext = context as FedPegTransactionBuildContext;

                context.TransactionBuilder
                    .Then()
                    .Send(fedPegTransactionBuildContext.OpReturnScript, Money.Zero);
            }

            // build transaction
            context.Transaction = context.TransactionBuilder.BuildTransaction(context.Sign);

            if (!context.TransactionBuilder.Verify(context.Transaction, out TransactionPolicyError[] errors))
            {
                string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
                this.logger.LogError($"Build transaction failed: {errorsMessage}");
                throw new WalletException($"Could not build the transaction. Details: {errorsMessage}");
            }
            return context.Transaction;
        }
    }
}
