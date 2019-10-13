using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.MempoolRules
{
    public class CheckMinGasLimitSmartContractMempoolRule : MempoolRule
    {
        private readonly ICallDataSerializer callDataSerializer;

        public CheckMinGasLimitSmartContractMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ICallDataSerializer callDataSerializer,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.callDataSerializer = callDataSerializer;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            Transaction transaction = context.Transaction;

            if (!transaction.IsSmartContractExecTransaction())
                return;

            // We know it has passed SmartContractFormatRule so we can deserialize it easily.
            TxOut scTxOut = transaction.TryGetSmartContractTxOut();
            Result<ContractTxData> callDataDeserializationResult = this.callDataSerializer.Deserialize(scTxOut.ScriptPubKey.ToBytes());
            ContractTxData callData = callDataDeserializationResult.Value;
            if (callData.GasPrice < SmartContractMempoolValidator.MinGasPrice)
                context.State.Fail(MempoolErrors.InsufficientFee, $"Gas price {callData.GasPrice} is below required price: {SmartContractMempoolValidator.MinGasPrice}").Throw();
        }
    }
}