﻿using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ContractSigning;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    /// <summary>
    /// Shared functionality for building SC transactions.
    /// </summary>
    public class SmartContractTransactionService : ISmartContractTransactionService
    {
        private const int MinConfirmationsAllChecks = 0;

        private const string SenderNoBalanceError = "The 'Sender' address you're trying to spend from doesn't have a balance available to spend. Please check the address and try again.";
        private readonly Network network;
        private readonly IWalletManager walletManager;
        private readonly IWalletTransactionHandler walletTransactionHandler;
        private readonly IMethodParameterStringSerializer methodParameterStringSerializer;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly IAddressGenerator addressGenerator;

        public SmartContractTransactionService(
            Network network,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IMethodParameterStringSerializer methodParameterStringSerializer,
            ICallDataSerializer callDataSerializer,
            IAddressGenerator addressGenerator)
        {
            this.network = network;
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.methodParameterStringSerializer = methodParameterStringSerializer;
            this.callDataSerializer = callDataSerializer;
            this.addressGenerator = addressGenerator;
        }

        public BuildCallContractTransactionResponse BuildCallTx(BuildCallContractTransactionRequest request)
        {
            AddressBalance addressBalance = this.walletManager.GetAddressBalance(request.Sender);
            if (addressBalance.AmountConfirmed == 0 && addressBalance.AmountUnconfirmed == 0)
                return BuildCallContractTransactionResponse.Failed(SenderNoBalanceError);

            var selectedInputs = new List<OutPoint>();
            selectedInputs = this.walletManager.GetSpendableInputsForAddress(request.WalletName, request.Sender);

            bool selectInputsSuccess = this.ReduceToRequestedInputs(request.Outpoints, selectedInputs);
            if (!selectInputsSuccess)
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
            AddressBalance addressBalance = this.walletManager.GetAddressBalance(request.Sender);
            if (addressBalance.AmountConfirmed == 0 && addressBalance.AmountUnconfirmed == 0)
                return BuildCreateContractTransactionResponse.Failed(SenderNoBalanceError);

            var selectedInputs = new List<OutPoint>();
            selectedInputs = this.walletManager.GetSpendableInputsForAddress(request.WalletName, request.Sender);

            bool selectInputsSuccess = this.ReduceToRequestedInputs(request.Outpoints, selectedInputs);
            if (!selectInputsSuccess)
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

        /// <summary>
        /// Reduces the selectedInputs to consist of only those asked for by the request, or leaves them the same if none were requested.
        /// </summary>
        private bool ReduceToRequestedInputs(List<OutpointRequestModel> requestedOutpoints, List<OutPoint> selectedInputs)
        {
            if (requestedOutpoints != null && requestedOutpoints.Any())
            {
                //Convert outpointRequest to OutPoint
                IEnumerable<OutPoint> requestedOutPoints = requestedOutpoints.Select(outPointRequest => new OutPoint(new uint256(outPointRequest.TransactionId), outPointRequest.Index));

                for (int i = selectedInputs.Count - 1; i >= 0; i--)
                {
                    if (!requestedOutPoints.Contains(selectedInputs[i]))
                        selectedInputs.RemoveAt(i);
                }

                if (!selectedInputs.Any())
                    return false;
            }

            return true;
        }

    }
}