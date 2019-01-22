using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.RuntimeObserver;

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
        private readonly CoinType coinType;
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
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.addressGenerator = addressGenerator;

        }

        public BuildCallContractTransactionResponse BuildCallTx(BuildCallContractTransactionRequest request)
        {
            AddressBalance addressBalance = this.walletManager.GetAddressBalance(request.Sender);
            if (addressBalance.AmountConfirmed == 0 && addressBalance.AmountUnconfirmed == 0)
                return BuildCallContractTransactionResponse.Failed(SenderNoBalanceError);

            var selectedInputs = new List<OutPoint>();
            selectedInputs = this.walletManager.GetSpendableInputsForAddress(request.WalletName, request.Sender);

            uint160 addressNumeric = request.ContractAddress.ToUint160(this.network);

            ContractTxData txData;
            if (request.Parameters != null && request.Parameters.Any())
            {
                try
                {
                    var methodParameters = this.methodParameterStringSerializer.Deserialize(request.Parameters);
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
                HdAccount account = wallet.GetAccountByCoinType(request.AccountName, this.coinType);
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

            ContractTxData txData;
            if (request.Parameters != null && request.Parameters.Any())
            {
                try
                {
                    var methodParameters = this.methodParameterStringSerializer.Deserialize(request.Parameters);
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
                HdAccount account = wallet.GetAccountByCoinType(request.AccountName, this.coinType);
                senderAddress = account.GetCombinedAddresses().FirstOrDefault(x => x.Address == request.Sender);
            }

            ulong totalFee = (request.GasPrice * request.GasLimit) + Money.Parse(request.FeeAmount);
            var walletAccountReference = new WalletAccountReference(request.WalletName, request.AccountName);
            var recipient = new Recipient { Amount = request.Amount ?? "0", ScriptPubKey = new Script(this.callDataSerializer.Serialize(txData)) };
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

    }
}