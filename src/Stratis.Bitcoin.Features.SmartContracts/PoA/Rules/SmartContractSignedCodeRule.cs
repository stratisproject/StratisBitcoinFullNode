using System;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.ContractSigning;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA.Rules
{
    /// <summary>
    /// Validates that the supplied smart contract code is signed with a valid signature.
    /// </summary>
    public class SmartContractSignedCodeRule : PartialValidationConsensusRule, ISmartContractMempoolRule
    {
        private readonly SignedCodeCallDataSerializer callDataSerializer;
        private readonly IContractSigner contractSigner;
        private readonly PubKey signingContractPubKey;

        public SmartContractSignedCodeRule(
            SignedCodeCallDataSerializer callDataSerializer,
            IContractSigner contractSigner,
            PubKey signingContractPubKey)
        {
            this.callDataSerializer = callDataSerializer;
            this.contractSigner = contractSigner;
            this.signingContractPubKey = signingContractPubKey;
        }

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            foreach (Transaction transaction in block.Transactions)
            {
                this.CheckTransaction(transaction);
            }

            return Task.CompletedTask;
        }

        public void CheckTransaction(MempoolValidationContext context)
        {
            CheckTransaction(context.Transaction);
        }

        private void CheckTransaction(Transaction transaction)
        {
            TxOut scTxOut = transaction.TryGetSmartContractTxOut();

            if (scTxOut == null)
            {
                return;
            }

            Result<ContractTxData> callDataDeserializationResult = this.callDataSerializer.Deserialize(scTxOut.ScriptPubKey.ToBytes());

            if (callDataDeserializationResult.IsFailure)
            {
                new ConsensusError("invalid-calldata-format", string.Format("Invalid {0} format", typeof(SignedCodeContractTxData).Name)).Throw();
            }

            SignedCodeContractTxData callData = callDataDeserializationResult.Value as SignedCodeContractTxData;

            if (callData == null)
            {
                new ConsensusError("invalid-calldata-format", string.Format("Invalid {0} format", typeof(SignedCodeContractTxData).Name)).Throw();
            }

            if (!callData.IsCreateContract)
            {
                // We do not need to validate calls
                return;
            }

            if (!this.contractSigner.Verify(this.signingContractPubKey, callData.ContractExecutionCode, callData.CodeSignature))
            {
                this.ThrowInvalidCode();
            }
        }
        
        private void ThrowInvalidCode()
        {
            new ConsensusError("contract-code-invalid-signature", "Contract code does not have a valid signature").Throw();
        }
    }
}