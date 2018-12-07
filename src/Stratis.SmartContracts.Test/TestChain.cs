using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.Tests.Common.MockChain;

namespace Stratis.SmartContracts.Test
{
    public class TestChain : IDisposable //: ITestChain
    {
        private const int AddressesToGenerate = 5;
        private const int AmountToPreload = 100_000;
        private static readonly Mnemonic SharedWalletMnemonic = new Mnemonic("lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom");

        private readonly PoAMockChain chain;
        private readonly IMethodParameterStringSerializer paramSerializer;

        private MockChainNode FirstNode => this.chain.Nodes[0];

        public IReadOnlyList<Base58Address> PreloadedAddresses { get; private set; }

        public TestChain()
        {
            this.chain = new PoAMockChain(2, SharedWalletMnemonic);
            this.paramSerializer = new MethodParameterStringSerializer(new SmartContractsPoARegTest()); // TODO: Inject
        }

        public TestChain Initialize()
        {
            this.chain.Build();

            // Get premine
            this.chain.MineBlocks(10);

            List<Base58Address> preloadedAddresses = new List<Base58Address>();

            for (int i = 0; i < AddressesToGenerate; i++)
            {
                HdAddress address = this.chain.Nodes[1].GetUnusedAddress();
                this.FirstNode.SendTransaction(address.ScriptPubKey, new Money(AmountToPreload, MoneyUnit.BTC));
                this.WaitForMempoolCountOnAllNodes(1);
                this.chain.MineBlocks(1);
                preloadedAddresses.Add(new Base58Address(address.Address));
            }

            this.PreloadedAddresses = preloadedAddresses;

            return this;
        }

        public ulong GetBalanceInStratoshis(Base58Address address)
        {
            // In case it's a contract address
            ulong contractBalance = this.FirstNode.GetContractBalance(address);
            if (contractBalance > 0)
            {
                return contractBalance;
            }

            return this.FirstNode.GetWalletAddressBalance(address);
        }

        public void MineBlocks(int num)
        {
            this.chain.MineBlocks(1);
        }

        public byte[] GetCode(Base58Address contractAddress)
        {
            return this.FirstNode.GetCode(contractAddress.Value);
        }

        public ReceiptResponse GetReceipt(uint256 txHash)
        {
            return this.FirstNode.GetReceipt(txHash.ToString());
        }

        public NBitcoin.Block GetLastBlock()
        {
            return this.FirstNode.GetLastBlock();
        }

        public SendCreateContractResult SendCreateContractTransaction(Base58Address from, byte[] contractCode, double amount,
            object[] parameters = null, ulong gasLimit = 50000, ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            double feeAmount = 0.01)
        {
            if (parameters == null)
            {
                parameters = new object[0];
            }

            string[] stringParameters = parameters.Select(this.GetSerializedStringForObject).ToArray();
            BuildCreateContractTransactionResponse response = this.FirstNode.SendCreateContractTransaction(contractCode, amount, stringParameters, gasLimit, gasPrice, feeAmount, from);

            if (response.Success)
            {
                WaitForMempoolCountOnAllNodes(1);
            }

            return new SendCreateContractResult
            {
                Fee = response.Fee,
                Hex = response.Hex,
                Message = response.Message,
                NewContractAddress = new Base58Address(response.NewContractAddress),
                Success = response.Success,
                TransactionId = response.TransactionId
            };
        }

        public SendCallContractResult SendCallContractTransaction(Base58Address from, string methodName,
            Base58Address contractAddress, double amount, object[] parameters = null, ulong gasLimit = 50000,
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice, double feeAmount = 0.01)
        {
            if (parameters == null)
            {
                parameters = new object[0];
            }

            string[] stringParameters = parameters.Select(this.GetSerializedStringForObject).ToArray();
            BuildCallContractTransactionResponse response = this.FirstNode.SendCallContractTransaction(methodName, contractAddress, amount, stringParameters, gasLimit, gasPrice, feeAmount, from);

            if (response.Success)
            {
                this.WaitForMempoolCountOnAllNodes(1);
            }

            return new SendCallContractResult
            {
                Fee = response.Fee,
                Hex = response.Hex,
                Message = response.Message,
                Success = response.Success,
                TransactionId = response.TransactionId
            };
        }

        public ILocalExecutionResult CallContractMethodLocally(Base58Address from, string methodName, Base58Address contractAddress,
            double amount, object[] parameters = null, ulong gasLimit = 50000,
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice, double feeAmount = 0.01)
        {
            if (parameters == null)
            {
                parameters = new object[0];
            }

            string[] stringParameters = parameters.Select(this.GetSerializedStringForObject).ToArray();
            return this.FirstNode.CallContractMethodLocally(methodName, contractAddress, amount, stringParameters, gasLimit, gasPrice, from);
        }

        private string GetSerializedStringForObject(object toSerialize)
        {
            // Convert to address before sending this
            if (toSerialize is Base58Address b)
            {
                return this.paramSerializer.Serialize(b.Value.ToAddress(this.FirstNode.CoreNode.FullNode.Network));
            }

            return this.paramSerializer.Serialize(toSerialize);
        }

        private void WaitForMempoolCountOnAllNodes(int num)
        {
            foreach (MockChainNode node in this.chain.Nodes)
            {
                node.WaitMempoolCount(num);
            }
        }

        public void Dispose()
        {
            this.chain.Dispose();
        }
    }
}
