using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.ContractSigning;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA.MempoolRules
{
    /// <summary>
    /// Validates that the supplied smart contract code is signed with a valid signature.
    /// Depends on <see cref="SignedCodeCallDataSerializer"/> being injected into the node.
    /// </summary>
    public class ContractSignedCodeLogicMempoolRule : MempoolRule
    {
        private readonly ICallDataSerializer callDataSerializer;
        private readonly IContractSigner contractSigner;
        private readonly PubKey signingContractPubKey;

        // TODO: Add this rule into the signed contract network definitions for tests (don't think it's used in production)

        public ContractSignedCodeLogicMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            ICallDataSerializer callDataSerializer,
            IContractSigner contractSigner,
            PubKey signingContractPubKey) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.callDataSerializer = callDataSerializer;
            this.contractSigner = contractSigner;
            this.signingContractPubKey = signingContractPubKey;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            TxOut scTxOut = context.Transaction.TryGetSmartContractTxOut();

            if (scTxOut == null)
            {
                // No SC output to validate.
                return;
            }

            ContractTxData txData = ContractTransactionChecker.GetContractTxData(this.callDataSerializer, scTxOut);

            if (!txData.IsCreateContract)
                return;

            ContractSignedCodeLogic.Check(txData, this.contractSigner, this.signingContractPubKey);
        }
    }
}