using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.SidechainWallet
{
    /// <inheritdoc />
    public class WalletTransactionHandler : Wallet.WalletTransactionHandler
    {
        public WalletTransactionHandler(ILoggerFactory loggerFactory, IWalletManager walletManager,
                                        IWalletFeePolicy walletFeePolicy, Network network) 
            : base(loggerFactory, walletManager, walletFeePolicy, network)
        {
            this.Network = network;
        }

        public Network Network { get; }

        public virtual Transaction BuildTransaction(TransactionBuildContext context)
        {
            this.InitializeTransactionBuilder(context);

            if (context.Shuffle)
            {
                context.TransactionBuilder.Shuffle();
            }

            // build transaction
            context.Transaction = context.TransactionBuilder.BuildCrossChainTransaction(context, this.Network);

            VerifyTransaction(context);
            
            return context.Transaction;
        }
    }
}
