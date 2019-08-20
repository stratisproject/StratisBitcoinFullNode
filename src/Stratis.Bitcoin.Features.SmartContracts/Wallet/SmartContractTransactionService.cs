using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ContractSigning;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    /// <summary>
    /// Shared functionality for building SC transactions.
    /// </summary>
    public class SmartContractTransactionService : ISmartContractTransactionService
    {
        private const int MinConfirmationsAllChecks = 0;

        private const string SenderNoBalanceError = "The 'Sender' address you're trying to spend from doesn't have a balance available to spend. Please check the address and try again.";
        public const string TransferFundsToContractError = "Can't transfer funds to contract.";
        public const string SenderNotInWalletError = "Address not found in wallet.";
        public const string AccountNotInWalletError = "No account.";
        public const string InsufficientBalanceError = "Insufficient balance.";
        public const string InvalidOutpointsError = "Invalid outpoints.";

        private readonly Network network;
        private readonly IWalletManager walletManager;
        private readonly IWalletTransactionHandler walletTransactionHandler;
        private readonly IMethodParameterStringSerializer methodParameterStringSerializer;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly IAddressGenerator addressGenerator;
        private readonly IStateRepositoryRoot stateRoot;

        public SmartContractTransactionService(
            Network network,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IMethodParameterStringSerializer methodParameterStringSerializer,
            ICallDataSerializer callDataSerializer,
            IAddressGenerator addressGenerator,
            IStateRepositoryRoot stateRoot)
        {
            this.network = network;
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.methodParameterStringSerializer = methodParameterStringSerializer;
            this.callDataSerializer = callDataSerializer;
            this.addressGenerator = addressGenerator;
            this.stateRoot = stateRoot;
        }

        public EstimateFeeResult EstimateFee(ScTxFeeEstimateRequest request)
        {
            Features.Wallet.Wallet wallet = this.walletManager.GetWallet(request.WalletName);

            HdAccount account = wallet.GetAccount(request.AccountName);

            if (account == null)
                return EstimateFeeResult.Failure(AccountNotInWalletError, $"No account with the name '{request.AccountName}' could be found.");

            HdAddress senderAddress = account.GetCombinedAddresses().FirstOrDefault(x => x.Address == request.Sender);

            if (senderAddress == null)
            {
                return EstimateFeeResult.Failure(SenderNotInWalletError, $"The given address {request.Sender} was not found in the wallet.");
            }

            if (!this.CheckBalance(senderAddress.Address))
                return EstimateFeeResult.Failure(InsufficientBalanceError, SenderNoBalanceError);

            List<OutPoint> selectedInputs = this.SelectInputs(request.WalletName, request.Sender, request.Outpoints);

            if (!selectedInputs.Any())
                return EstimateFeeResult.Failure(InvalidOutpointsError, "Invalid list of request outpoints have been passed to the method. Please ensure that the outpoints are spendable by the sender address.");

            var recipients = new List<Recipient>();
            foreach (RecipientModel recipientModel in request.Recipients)
            {
                uint160 address = recipientModel.DestinationAddress.ToUint160(this.network);

                if (this.stateRoot.IsExist(address))
                {
                    return EstimateFeeResult.Failure(TransferFundsToContractError, $"The recipient address {recipientModel.DestinationAddress} is a contract. Transferring funds directly to a contract is not supported.");
                }

                recipients.Add(new Recipient
                {
                    ScriptPubKey = BitcoinAddress.Create(recipientModel.DestinationAddress, this.network).ScriptPubKey,
                    Amount = recipientModel.Amount
                });
            }

            // Build context
            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
                MinConfirmations = MinConfirmationsAllChecks,
                Shuffle = false,
                OpReturnData = request.OpReturnData,
                OpReturnAmount = string.IsNullOrEmpty(request.OpReturnAmount) ? null : Money.Parse(request.OpReturnAmount),
                SelectedInputs = selectedInputs,
                AllowOtherInputs = false,
                Recipients = recipients,
                ChangeAddress = senderAddress,

                // Unique for fee estimation
                TransactionFee = null,
                FeeType = FeeParser.Parse(request.FeeType),
                Sign = false,                
            };

            Money fee = this.walletTransactionHandler.EstimateFee(context);

            return EstimateFeeResult.Success(fee);
        }

        public BuildContractTransactionResult BuildTx(BuildContractTransactionRequest request)
        {
            Features.Wallet.Wallet wallet = this.walletManager.GetWallet(request.WalletName);

            HdAccount account = wallet.GetAccount(request.AccountName);

            if (account == null)
                return BuildContractTransactionResult.Failure(AccountNotInWalletError, $"No account with the name '{request.AccountName}' could be found.");

            HdAddress senderAddress = account.GetCombinedAddresses().FirstOrDefault(x => x.Address == request.Sender);

            if (senderAddress == null)
            {
                return BuildContractTransactionResult.Failure(SenderNotInWalletError, $"The given address {request.Sender} was not found in the wallet.");
            }

            if (!this.CheckBalance(senderAddress.Address))
                return BuildContractTransactionResult.Failure(InsufficientBalanceError, SenderNoBalanceError);

            List<OutPoint> selectedInputs = this.SelectInputs(request.WalletName, request.Sender, request.Outpoints);

            if (!selectedInputs.Any())
                return BuildContractTransactionResult.Failure(InvalidOutpointsError, "Invalid list of request outpoints have been passed to the method. Please ensure that the outpoints are spendable by the sender address.");
            
            var recipients = new List<Recipient>();
            foreach (RecipientModel recipientModel in request.Recipients)
            {
                uint160 address = recipientModel.DestinationAddress.ToUint160(this.network);

                if (this.stateRoot.IsExist(address))
                {
                    return BuildContractTransactionResult.Failure(TransferFundsToContractError, $"The recipient address {recipientModel.DestinationAddress} is a contract. Transferring funds directly to a contract is not supported.");
                }

                recipients.Add(new Recipient
                {
                    ScriptPubKey = BitcoinAddress.Create(recipientModel.DestinationAddress, this.network).ScriptPubKey,
                    Amount = recipientModel.Amount
                });
            }

            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
                TransactionFee = string.IsNullOrEmpty(request.FeeAmount) ? null : Money.Parse(request.FeeAmount),
                MinConfirmations = MinConfirmationsAllChecks,
                Shuffle = false,
                OpReturnData = request.OpReturnData,
                OpReturnAmount = string.IsNullOrEmpty(request.OpReturnAmount) ? null : Money.Parse(request.OpReturnAmount),
                WalletPassword = request.Password,
                SelectedInputs = selectedInputs,
                AllowOtherInputs = false,
                Recipients = recipients,
                ChangeAddress = senderAddress
            };

            Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);

            var model = new WalletBuildTransactionModel
            {
                Hex = transaction.ToHex(),
                Fee = context.TransactionFee,
                TransactionId = transaction.GetHash()
            };

            return BuildContractTransactionResult.Success(model);
        }

        public BuildCallContractTransactionResponse BuildCallTx(BuildCallContractTransactionRequest request)
        {
            if(!this.CheckBalance(request.Sender))
                return BuildCallContractTransactionResponse.Failed(SenderNoBalanceError);

            List<OutPoint> selectedInputs = this.SelectInputs(request.WalletName, request.Sender, request.Outpoints);
            if (!selectedInputs.Any())
                return BuildCallContractTransactionResponse.Failed("Invalid list of request outpoints have been passed to the method. Please ensure that the outpoints are spendable by the sender address");

            uint160 addressNumeric = request.ContractAddress.ToUint160(this.network);

            ContractTxData txData;
            if (request.Parameters != null && request.Parameters.Any())
            {
                try
                {
                    object[] methodParameters = this.methodParameterStringSerializer.Deserialize(request.Parameters);
                    txData = new ContractTxData(ReflectionVirtualMachine.VmVersion, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasPrice, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasLimit, addressNumeric, request.MethodName, methodParameters);
                }
                catch (MethodParameterStringSerializerException exception)
                {
                    return BuildCallContractTransactionResponse.Failed(exception.Message);
                }
            }
            else
            {
                txData = new ContractTxData(ReflectionVirtualMachine.VmVersion, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasPrice, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasLimit, addressNumeric, request.MethodName);
            }

            HdAddress senderAddress = null;
            if (!string.IsNullOrWhiteSpace(request.Sender))
            {
                Features.Wallet.Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                HdAccount account = wallet.GetAccount(request.AccountName);
                if (account == null)
                    return BuildCallContractTransactionResponse.Failed($"No account with the name '{request.AccountName}' could be found.");

                senderAddress = account.GetCombinedAddresses().FirstOrDefault(x => x.Address == request.Sender);
            }

            ulong totalFee = (request.GasPrice * request.GasLimit) + Money.Parse(request.FeeAmount);
            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
                TransactionFee = totalFee,
                ChangeAddress = senderAddress,
                SelectedInputs = selectedInputs,
                MinConfirmations = MinConfirmationsAllChecks,
                WalletPassword = request.Password,
                Recipients = new[] { new Recipient { Amount = request.Amount, ScriptPubKey = new Script(this.callDataSerializer.Serialize(txData)) } }.ToList()
            };

            try
            {
                Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);
                return BuildCallContractTransactionResponse.Succeeded(request.MethodName, transaction, context.TransactionFee);
            }
            catch (Exception exception)
            {
                return BuildCallContractTransactionResponse.Failed(exception.Message);
            }
        }

        public BuildCreateContractTransactionResponse BuildCreateTx(BuildCreateContractTransactionRequest request)
        {
            if(!this.CheckBalance(request.Sender))
                return BuildCreateContractTransactionResponse.Failed(SenderNoBalanceError);

            List<OutPoint> selectedInputs = this.SelectInputs(request.WalletName, request.Sender, request.Outpoints);
            if (!selectedInputs.Any())
                return BuildCreateContractTransactionResponse.Failed("Invalid list of request outpoints have been passed to the method. Please ensure that the outpoints are spendable by the sender address");

            ContractTxData txData;
            if (request.Parameters != null && request.Parameters.Any())
            {
                try
                {
                    object[] methodParameters = this.methodParameterStringSerializer.Deserialize(request.Parameters);
                    txData = new ContractTxData(ReflectionVirtualMachine.VmVersion, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasPrice, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasLimit, request.ContractCode.HexToByteArray(), methodParameters);
                }
                catch (MethodParameterStringSerializerException exception)
                {
                    return BuildCreateContractTransactionResponse.Failed(exception.Message);
                }
            }
            else
            {
                txData = new ContractTxData(ReflectionVirtualMachine.VmVersion, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasPrice, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasLimit, request.ContractCode.HexToByteArray());
            }

            HdAddress senderAddress = null;
            if (!string.IsNullOrWhiteSpace(request.Sender))
            {
                Features.Wallet.Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                HdAccount account = wallet.GetAccount(request.AccountName);
                if (account == null)
                    return BuildCreateContractTransactionResponse.Failed($"No account with the name '{request.AccountName}' could be found.");

                senderAddress = account.GetCombinedAddresses().FirstOrDefault(x => x.Address == request.Sender);
            }

            ulong totalFee = (request.GasPrice * request.GasLimit) + Money.Parse(request.FeeAmount);
            var walletAccountReference = new WalletAccountReference(request.WalletName, request.AccountName);

            byte[] serializedTxData = this.callDataSerializer.Serialize(txData);

            Result<ContractTxData> deserialized = this.callDataSerializer.Deserialize(serializedTxData);

            // We also want to ensure we're sending valid data: AKA it can be deserialized.
            if (deserialized.IsFailure)
            {
                return BuildCreateContractTransactionResponse.Failed("Invalid data. If network requires code signing, check the code contains a signature.");
            }

            // HACK
            // If requiring a signature, also check the signature.
            if (this.network is ISignedCodePubKeyHolder holder)
            {
                var signedTxData = (SignedCodeContractTxData) deserialized.Value;
                bool validSig =new ContractSigner().Verify(holder.SigningContractPubKey, signedTxData.ContractExecutionCode, signedTxData.CodeSignature);

                if (!validSig)
                {
                    return BuildCreateContractTransactionResponse.Failed("Signature in code does not come from required signing key.");
                }
            }

            var recipient = new Recipient { Amount = request.Amount ?? "0", ScriptPubKey = new Script(serializedTxData) };
            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = walletAccountReference,
                TransactionFee = totalFee,
                ChangeAddress = senderAddress,
                SelectedInputs = selectedInputs,
                MinConfirmations = MinConfirmationsAllChecks,
                WalletPassword = request.Password,
                Recipients = new[] { recipient }.ToList()
            };

            try
            {
                Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);
                uint160 contractAddress = this.addressGenerator.GenerateAddress(transaction.GetHash(), 0);
                return BuildCreateContractTransactionResponse.Succeeded(transaction, context.TransactionFee, contractAddress.ToBase58Address(this.network));
            }
            catch (Exception exception)
            {
                return BuildCreateContractTransactionResponse.Failed(exception.Message);
            }
        }

        public ContractTxData BuildLocalCallTxData(LocalCallContractRequest request)
        {
            uint160 contractAddress = request.ContractAddress.ToUint160(this.network);

            if (request.Parameters != null && request.Parameters.Any())
            {
                object[] methodParameters = this.methodParameterStringSerializer.Deserialize(request.Parameters);

                return new ContractTxData(ReflectionVirtualMachine.VmVersion, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasPrice, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasLimit, contractAddress, request.MethodName, methodParameters);
            }

            return new ContractTxData(ReflectionVirtualMachine.VmVersion, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasPrice, (Stratis.SmartContracts.RuntimeObserver.Gas)request.GasLimit, contractAddress, request.MethodName);
        }

        private bool CheckBalance(string address)
        {
            AddressBalance addressBalance = this.walletManager.GetAddressBalance(address);
            return !(addressBalance.AmountConfirmed == 0 && addressBalance.AmountUnconfirmed == 0);
        }

        private List<OutPoint> SelectInputs(string walletName, string sender, List<OutpointRequest> outpoints)
        {
            List<OutPoint> selectedInputs = this.walletManager.GetSpendableInputsForAddress(walletName, sender);

            return this.ReduceToRequestedInputs(outpoints, selectedInputs);
        }

        /// <summary>
        /// Reduces the selectedInputs to consist of only those asked for by the request, or leaves them the same if none were requested.
        /// </summary>
        /// <returns>The new list of outpoints.</returns>
        private List<OutPoint> ReduceToRequestedInputs(IReadOnlyList<OutpointRequest> requestedOutpoints, IReadOnlyList<OutPoint> selectedInputs)
        {
            var result = new List<OutPoint>(selectedInputs);

            if (requestedOutpoints != null && requestedOutpoints.Any())
            {
                //Convert outpointRequest to OutPoint
                IEnumerable<OutPoint> requestedOutPoints = requestedOutpoints.Select(outPointRequest => new OutPoint(new uint256(outPointRequest.TransactionId), outPointRequest.Index));

                for (int i = result.Count - 1; i >= 0; i--)
                {
                    if (!requestedOutPoints.Contains(result[i]))
                        result.RemoveAt(i);
                }
            }

            return result;
        }
    }
}
