using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.SmartContracts.Executor.Reflection.Local;
using Stratis.SmartContracts.Tests.Common.MockChain;

namespace Stratis.SmartContracts.Test
{
    public class TestChain : IDisposable //: ITestChain
    {
        private const int AddressesToGenerate = 5;
        private const int AmountToPreload = 100_000;
        private static readonly Mnemonic SharedWalletMnemonic = new Mnemonic("lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom");
        private readonly PoAMockChain chain;

        public TestChain()
        {
            this.chain = new PoAMockChain(2, SharedWalletMnemonic);
        }

        public IReadOnlyList<Base58Address> PreloadedAddresses { get; private set; }

        public TestChain Initialize()
        {
            this.chain.Build();

            // Get premine
            this.chain.MineBlocks(10);

            List<Base58Address> preloadedAddresses = new List<Base58Address>();

            for (int i = 0; i < AddressesToGenerate; i++)
            {
                HdAddress address = this.chain.Nodes[1].GetUnusedAddress();
                this.chain.Nodes[0].SendTransaction(address.ScriptPubKey, new Money(AmountToPreload, MoneyUnit.BTC));
                this.chain.Nodes[0].WaitMempoolCount(1);
                this.chain.MineBlocks(1);
                preloadedAddresses.Add(new Base58Address(address.Address));
            }

            this.PreloadedAddresses = preloadedAddresses;

            return this;
        }

        public ulong GetBalanceInStratoshis(Base58Address address)
        {
            throw new NotImplementedException();
        }

        public void MineBlocks(int num)
        {
            throw new NotImplementedException();
        }

        public byte[] GetCode(Base58Address contractAddress)
        {
            throw new NotImplementedException();
        }

        public ReceiptResponse GetReceipt(uint256 txHash)
        {
            throw new NotImplementedException();
        }

        public Block GetLastBlock()
        {
            throw new NotImplementedException();
        }

        public SendCreateContractResult SendCreateContractTransaction(Base58Address @from, byte[] contractCode, double amount,
            string[] parameters = null, ulong gasLimit = 50000, ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            double feeAmount = 0.01)
        {
            throw new NotImplementedException();
        }

        public SendCallContractResult SendCallContractTransaction(Base58Address @from, string methodName,
            Base58Address contractAddress, double amount, string[] parameters = null, ulong gasLimit = 50000,
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice, double feeAmount = 0.01)
        {
            throw new NotImplementedException();
        }

        public ILocalExecutionResult CallContractMethodLocally(Base58Address @from, string methodName, Base58Address contractAddress,
            double amount, string[] parameters = null, ulong gasLimit = 50000,
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice, double feeAmount = 0.01)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            this.chain.Dispose();
        }
    }
}
