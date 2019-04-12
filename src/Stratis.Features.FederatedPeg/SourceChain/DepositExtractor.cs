﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg.SourceChain
{
    public class DepositExtractor : IDepositExtractor
    {
        /// <summary>
        /// This deposit extractor implementation only looks for a very specific deposit format.
        /// The deposits must have 2 outputs.
        /// </summary>
        private const int ExpectedNumberOfOutputs = 2;

        private readonly IOpReturnDataReader opReturnDataReader;

        private readonly ILogger logger;

        private readonly IFederationGatewaySettings settings;

        private readonly Script depositScript;

        public uint MinimumDepositConfirmations { get; private set; }

        public DepositExtractor(
            ILoggerFactory loggerFactory,
            IFederationGatewaySettings federationGatewaySettings,
            IOpReturnDataReader opReturnDataReader)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            // Note: MultiSigRedeemScript.PaymentScript equals MultiSigAddress.ScriptPubKey
            this.depositScript =
                federationGatewaySettings?.MultiSigRedeemScript?.PaymentScript ??
                federationGatewaySettings?.MultiSigAddress?.ScriptPubKey;
            this.opReturnDataReader = opReturnDataReader;
            this.settings = federationGatewaySettings;
            this.MinimumDepositConfirmations = federationGatewaySettings.MinimumDepositConfirmations;
        }

        /// <inheritdoc />
        public IReadOnlyList<IDeposit> ExtractDepositsFromBlock(Block block, int blockHeight)
        {
            var deposits = new List<IDeposit>();

            // If it's an empty block, there's no deposits inside.
            if (block.Transactions.Count <= 1)
                return deposits;

            uint256 blockHash = block.GetHash();

            foreach (Transaction transaction in block.Transactions)
            {
                IDeposit deposit = this.ExtractDepositFromTransaction(transaction, blockHeight, blockHash);
                if (deposit != null)
                {
                    deposits.Add(deposit);
                }
            }

            return deposits;
        }

        /// <inheritdoc />
        public IDeposit ExtractDepositFromTransaction(Transaction transaction, int blockHeight, uint256 blockHash)
        {
            // Coinbases can't have deposits.
            if (transaction.IsCoinBase)
                return null;

            // Deposits have a certain structure with 2 outputs. 
            if (transaction.Outputs.Count != ExpectedNumberOfOutputs)
                return null;

            List<TxOut> depositsToMultisig = transaction.Outputs.Where(output =>
                output.ScriptPubKey == this.depositScript
                && output.Value > this.settings.TransactionFee).ToList();

            if (!depositsToMultisig.Any())
                return null;

            if (!this.opReturnDataReader.TryGetTargetAddress(transaction, out string targetAddress))
                return null;

            this.logger.LogInformation("Processing a received deposit transaction with address: {0}. Transaction hash: {1}.",
                targetAddress, transaction.GetHash());

            return new Deposit(transaction.GetHash(), depositsToMultisig.Sum(o => o.Value), targetAddress, blockHeight, blockHash);
        }

        public MaturedBlockDepositsModel ExtractBlockDeposits(ChainedHeaderBlock newlyMaturedBlock)
        {
            if (newlyMaturedBlock == null)
                return null;

            var maturedBlock = new MaturedBlockInfoModel()
            {
                BlockHash = newlyMaturedBlock.ChainedHeader.HashBlock,
                BlockHeight = newlyMaturedBlock.ChainedHeader.Height,
                BlockTime = newlyMaturedBlock.ChainedHeader.Header.Time
            };

            IReadOnlyList<IDeposit> deposits =
                this.ExtractDepositsFromBlock(newlyMaturedBlock.Block, newlyMaturedBlock.ChainedHeader.Height);

            var maturedBlockDeposits = new MaturedBlockDepositsModel(maturedBlock, deposits);

            return maturedBlockDeposits;
        }
    }
}