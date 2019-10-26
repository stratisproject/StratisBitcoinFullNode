using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.SmartContracts.CLR.ResultProcessors;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ContractTransferProcessorTests
    {
        private readonly Network network;
        private readonly ILoggerFactory loggerFactory;
        private readonly ContractTransferProcessor transferProcessor;

        public ContractTransferProcessorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = new SmartContractsRegTest();
            this.transferProcessor = new ContractTransferProcessor(this.loggerFactory, this.network);
        }

        /*
         * The following are the possible scenarios going into condensing transaction creation:
         * 
         * 1) Contract has no balance, tx value = 0, transfer value = 0:
         *  DO NOTHING
         * 
         * 2) Contract has no balance, tx value > 0, transfer value = 0:
         *  ASSIGN CONTRACT CURRENT UTXO
         *  
         * 3) Contract has no balance, tx value = 0, transfer value > 0: 
         *  CAN'T HAPPEN
         *  
         * 4) Contract has no balance, tx value > 0, transfer value > 0:
         *  CREATE CONDENSING TX
         *  
         * 5) Contract has balance, tx value = 0, transfer value = 0:
         *  DO NOTHING
         *  
         * 6) Contract has balance, tx value > 0, transfer value = 0:
         *  CREATE CONDENSING TX
         *  
         * 7) Contract has balance, tx value = 0, transfer value > 0: 
         *  CREATE CONDENSING TX
         *  
         * 8) Contract has balance, tx value > 0, transfer value > 0
         *  CREATE CONDENSING TX
         *  
         */


        [Fact]
        public void NoBalance_TxValue0_TransferValue0()
        {
            uint160 contractAddress = new uint160(1);

            // No balance
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns<ContractUnspentOutput>(null);

            // No tx value
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);

            // No transfers
            var transfers = new List<TransferInfo>();

            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transfers, false);

            // Ensure no state changes were made and no transaction has been added
            Assert.Null(internalTransaction);
            stateMock.Verify(x => x.SetUnspent(It.IsAny<uint160>(), It.IsAny<ContractUnspentOutput>()), Times.Never);
        }

        [Fact]
        public void NoBalance_TxValue1_TransferValue0()
        {
            uint160 contractAddress = new uint160(1);

            // No balance
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns<ContractUnspentOutput>(null);

            // 100 tx value
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(100);

            // No transfers
            var transfers = new List<TransferInfo>();

            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transfers, false);

            // Ensure unspent was saved, but no condensing transaction was generated.
            Assert.Null(internalTransaction);
            stateMock.Verify(x => x.SetUnspent(contractAddress, It.IsAny<ContractUnspentOutput>()));
        }

        [Fact]
        public void NoBalance_TxValue0_TransferValue1()
        {
            uint160 contractAddress = new uint160(1);
            uint160 receiverAddress = new uint160(2);

            // No balance
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns<ContractUnspentOutput>(null);

            // No tx value
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);

            // A transfer of 100
            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo(contractAddress, receiverAddress, 100)
            };

            // This should be impossible - contract has no existing balance and didn't get sent anything so it cannot send value.
            // TODO: Could be more informative exception
            Assert.ThrowsAny<Exception>(() =>
            {
                Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transferInfos, false);
            });
        }

        [Fact]
        public void NoBalance_TxValue1_TransferValue1()
        {
            uint160 contractAddress = new uint160(1);
            uint160 receiverAddress = new uint160(2);

            // No balance
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetAccountState(contractAddress)).Returns(new AccountState
            {
                CodeHash = new byte[32],
                StateRoot = new byte[32],
                TypeName = "Mock",
                UnspentHash = new byte[32]
            });
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns<ContractUnspentOutput>(null);

            // tx value 100
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(100);
            txContextMock.SetupGet(p => p.TransactionHash).Returns(new uint256(123));
            txContextMock.SetupGet(p => p.Nvout).Returns(1);
            txContextMock.SetupGet(p => p.Time).Returns(12345);

            // transfer 75
            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo(contractAddress, receiverAddress, 75)
            };

            // Condensing tx generated. 1 input from tx and 2 outputs - 1 for each contract and receiver
            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transferInfos, false);
            Assert.NotNull(internalTransaction);
            Assert.Equal(txContextMock.Object.Time, internalTransaction.Time);
            Assert.Single(internalTransaction.Inputs);
            Assert.Equal(2, internalTransaction.Outputs.Count);
            Assert.Equal(txContextMock.Object.TransactionHash, internalTransaction.Inputs[0].PrevOut.Hash);
            Assert.Equal(txContextMock.Object.Nvout, internalTransaction.Inputs[0].PrevOut.N);
            string output1Address = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(internalTransaction.Outputs[0].ScriptPubKey).GetAddress(this.network).ToString();
            Assert.Equal(receiverAddress.ToBase58Address(this.network), output1Address);
            Assert.Equal(75, internalTransaction.Outputs[0].Value); // Note outputs are in descending order by value.
            Assert.True(internalTransaction.Outputs[1].ScriptPubKey.IsSmartContractInternalCall());
            Assert.Equal(25, internalTransaction.Outputs[1].Value);

            // Ensure db updated
            stateMock.Verify(x => x.SetUnspent(contractAddress, It.Is<ContractUnspentOutput>(unspent => unspent.Value == 25)), Times.Once);
        }

        [Fact]
        public void HasBalance_TxValue0_TransferValue0()
        {
            uint160 contractAddress = new uint160(1);

            // balance of 100
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns(new ContractUnspentOutput
            {
                Hash = new uint256(1),
                Nvout = 1,
                Value = 100
            });

            // No tx value
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);
            txContextMock.SetupGet(p => p.Time).Returns(12345);

            // No transfers
            var transfers = new List<TransferInfo>();

            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transfers, false);

            // Ensure no state changes were made and no transaction has been added
            Assert.Null(internalTransaction);
            stateMock.Verify(x => x.SetUnspent(It.IsAny<uint160>(), It.IsAny<ContractUnspentOutput>()), Times.Never);
        }

        [Fact]
        public void HasBalance_TxValue1_TransferValue0()
        {
            uint160 contractAddress = new uint160(1);

            // Has balance
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetAccountState(contractAddress)).Returns(new AccountState
            {
                CodeHash = new byte[32],
                StateRoot = new byte[32],
                TypeName = "Mock",
                UnspentHash = new byte[32]
            });
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns(new ContractUnspentOutput
            {
                Hash = new uint256(1),
                Nvout = 1,
                Value = 100
            });

            // tx value 100
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(100);
            txContextMock.SetupGet(p => p.TransactionHash).Returns(new uint256(123));
            txContextMock.SetupGet(p => p.Nvout).Returns(1);
            txContextMock.SetupGet(p => p.Time).Returns(12345);

            // no transfers
            var transferInfos = new List<TransferInfo>();

            // Condensing tx generated. 2 inputs. Current tx and stored spendable output. 1 output. 
            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transferInfos, false);
            Assert.NotNull(internalTransaction);
            Assert.Equal(txContextMock.Object.Time, internalTransaction.Time);
            Assert.Equal(2, internalTransaction.Inputs.Count);
            Assert.Single(internalTransaction.Outputs);
            Assert.Equal(txContextMock.Object.TransactionHash, internalTransaction.Inputs[0].PrevOut.Hash);
            Assert.Equal(txContextMock.Object.Nvout, internalTransaction.Inputs[0].PrevOut.N);
            Assert.Equal(new uint256(1), internalTransaction.Inputs[1].PrevOut.Hash);
            Assert.Equal((uint) 1, internalTransaction.Inputs[1].PrevOut.N);
            Assert.True(internalTransaction.Outputs[0].ScriptPubKey.IsSmartContractInternalCall());
            Assert.Equal(200, internalTransaction.Outputs[0].Value);

            // Ensure db updated
            stateMock.Verify(x => x.SetUnspent(contractAddress, It.Is<ContractUnspentOutput>(unspent => unspent.Value == 200)), Times.Once);
        }

        [Fact]
        public void HasBalance_TxValue0_TransferValue1()
        {
            uint160 contractAddress = new uint160(1);
            uint160 receiverAddress = new uint160(2);

            // Has balance
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetAccountState(contractAddress)).Returns(new AccountState
            {
                CodeHash = new byte[32],
                StateRoot = new byte[32],
                TypeName = "Mock",
                UnspentHash = new byte[32]
            });
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns(new ContractUnspentOutput
            {
                Hash = new uint256(1),
                Nvout = 1,
                Value = 100
            });

            // no tx value
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);
            txContextMock.SetupGet(p => p.Time).Returns(12345);

            // transfer 75
            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo(contractAddress, receiverAddress, 75)
            };

            // Condensing tx generated. 1 input. 2 outputs for each receiver and contract.
            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transferInfos, false);
            Assert.NotNull(internalTransaction);
            Assert.Equal(txContextMock.Object.Time, internalTransaction.Time);
            Assert.Single(internalTransaction.Inputs);
            Assert.Equal(2, internalTransaction.Outputs.Count);
            Assert.Equal(new uint256(1), internalTransaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint) 1, internalTransaction.Inputs[0].PrevOut.N);
            string output1Address = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(internalTransaction.Outputs[0].ScriptPubKey).GetAddress(this.network).ToString();
            Assert.Equal(receiverAddress.ToBase58Address(this.network), output1Address);
            Assert.Equal(75, internalTransaction.Outputs[0].Value);
            Assert.True(internalTransaction.Outputs[1].ScriptPubKey.IsSmartContractInternalCall());
            Assert.Equal(25, internalTransaction.Outputs[1].Value);

            // Ensure db updated
            stateMock.Verify(x => x.SetUnspent(contractAddress, It.Is<ContractUnspentOutput>(unspent => unspent.Value == 25)), Times.Once);
        }

        [Fact]
        public void HasBalance_TxValue1_TransferValue1()
        {
            uint160 contractAddress = new uint160(1);
            uint160 receiverAddress = new uint160(2);

            // Has balance
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetAccountState(contractAddress)).Returns(new AccountState
            {
                CodeHash = new byte[32],
                StateRoot = new byte[32],
                TypeName = "Mock",
                UnspentHash = new byte[32]
            });
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns(new ContractUnspentOutput
            {
                Hash = new uint256(1),
                Nvout = 1,
                Value = 100
            });

            // no tx value
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(100);
            txContextMock.SetupGet(p => p.TransactionHash).Returns(new uint256(123));
            txContextMock.SetupGet(p => p.Nvout).Returns(1);
            txContextMock.SetupGet(p => p.Time).Returns(12345);

            // transfer 75
            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo(contractAddress, receiverAddress, 75)
            };

            // Condensing tx generated. 2 inputs from currently stored utxo and current tx. 2 outputs for each receiver and contract.
            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transferInfos, false);
            Assert.NotNull(internalTransaction);
            Assert.Equal(txContextMock.Object.Time, internalTransaction.Time);
            Assert.Equal(2, internalTransaction.Inputs.Count);
            Assert.Equal(2, internalTransaction.Outputs.Count);
            Assert.Equal(new uint256(123), internalTransaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)1, internalTransaction.Inputs[0].PrevOut.N);
            Assert.Equal(new uint256(1), internalTransaction.Inputs[1].PrevOut.Hash);
            Assert.Equal((uint)1, internalTransaction.Inputs[1].PrevOut.N);

            Assert.True(internalTransaction.Outputs[0].ScriptPubKey.IsSmartContractInternalCall());
            Assert.Equal(125, internalTransaction.Outputs[0].Value);

            string output2Address = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(internalTransaction.Outputs[1].ScriptPubKey).GetAddress(this.network).ToString();
            Assert.Equal(receiverAddress.ToBase58Address(this.network), output2Address);
            Assert.Equal(75, internalTransaction.Outputs[1].Value);

            // Ensure db updated
            stateMock.Verify(x => x.SetUnspent(contractAddress, It.Is<ContractUnspentOutput>(unspent => unspent.Value == 125)), Times.Once);
        }

        [Fact]
        public void Transfers_With_0Value()
        {
            // Scenario where contract was not sent any funds, but did make a method call with value 0.
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetCode(It.IsAny<uint160>())).Returns<byte[]>(null);
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);

            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo(uint160.One, new uint160(2), 0)
            };

            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, uint160.One, txContextMock.Object, transferInfos, false);

            // No condensing transaction was generated.
            Assert.Null(internalTransaction);
        }

        [Fact]
        public void Transfers_Summed_Correctly()
        {
            uint160 contractAddress = new uint160(1);
            uint160 receiverAddress = new uint160(2);
            uint160 thirdAddress = new uint160(3);

            // Has balance
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetAccountState(contractAddress)).Returns(new AccountState
            {
                CodeHash = new byte[32],
                StateRoot = new byte[32],
                TypeName = "Mock",
                UnspentHash = new byte[32]
            });
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns(new ContractUnspentOutput
            {
                Hash = new uint256(1),
                Nvout = 1,
                Value = 100
            });

            // no tx value
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);
            txContextMock.SetupGet(p => p.Time).Returns(12345);

            // several transfers
            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo(contractAddress, receiverAddress, 75),
                new TransferInfo(receiverAddress, contractAddress, 20),
                new TransferInfo(receiverAddress, thirdAddress, 5)
            };

            // End result should be Contract: 45, Receiver: 50, ThirdAddress: 5

            // Condensing tx generated. 1 input. 3 outputs with consolidated balances.
            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transferInfos, false);
            Assert.NotNull(internalTransaction);
            Assert.Equal(txContextMock.Object.Time, internalTransaction.Time);
            Assert.Single(internalTransaction.Inputs);
            Assert.Equal(3, internalTransaction.Outputs.Count);
            Assert.Equal(new uint256(1), internalTransaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)1, internalTransaction.Inputs[0].PrevOut.N);
            string output1Address = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(internalTransaction.Outputs[0].ScriptPubKey).GetAddress(this.network).ToString();
            Assert.Equal(receiverAddress.ToBase58Address(this.network), output1Address);
            Assert.Equal(50, internalTransaction.Outputs[0].Value);
            Assert.True(internalTransaction.Outputs[1].ScriptPubKey.IsSmartContractInternalCall());
            Assert.Equal(45, internalTransaction.Outputs[1].Value);
            string output3Address = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(internalTransaction.Outputs[2].ScriptPubKey).GetAddress(this.network).ToString();
            Assert.Equal(thirdAddress.ToBase58Address(this.network), output3Address);
            Assert.Equal(5, internalTransaction.Outputs[2].Value);

            // Ensure db updated
            stateMock.Verify(x => x.SetUnspent(contractAddress, It.Is<ContractUnspentOutput>(unspent => unspent.Value == 45)), Times.Once);
        }

        [Fact]
        public void Execution_Failure_With_Value_No_Transfers_Creates_Refund()
        {
            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(100);
            txContextMock.SetupGet(p => p.TransactionHash).Returns(new uint256(123));
            txContextMock.SetupGet(p => p.Nvout).Returns(1);
            txContextMock.SetupGet(p => p.Sender).Returns(new uint160(2));
            txContextMock.SetupGet(p => p.Time).Returns(12345);

            Transaction refundTransaction = this.transferProcessor.Process(null, null, txContextMock.Object, null, true);

            Assert.Single(refundTransaction.Inputs);
            Assert.Single(refundTransaction.Outputs);
            Assert.Equal(new uint256(123), refundTransaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)1, refundTransaction.Inputs[0].PrevOut.N);
            Assert.Equal(txContextMock.Object.TxOutValue, (ulong) refundTransaction.Outputs[0].Value);
            string outputAddress = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(refundTransaction.Outputs[0].ScriptPubKey).GetAddress(this.network).ToString();

            Assert.Equal(txContextMock.Object.Sender.ToBase58Address(this.network), outputAddress);
            Assert.Equal(txContextMock.Object.Time, refundTransaction.Time);
        }

        [Fact]
        public void Execution_Failure_No_Value_With_Transfers_Does_Not_Transfer()
        {
            uint160 contractAddress = new uint160(1);
            uint160 receiverAddress = new uint160(2);
            uint160 thirdAddress = new uint160(3);

            var txContextMock = new Mock<IContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);
            txContextMock.SetupGet(p => p.TransactionHash).Returns(new uint256(123));
            txContextMock.SetupGet(p => p.Nvout).Returns(1);
            txContextMock.SetupGet(p => p.Sender).Returns(new uint160(2));
            txContextMock.SetupGet(p => p.Time).Returns(12345);

            // several transfers
            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo(contractAddress, receiverAddress, 75),
                new TransferInfo(receiverAddress, contractAddress, 20),
                new TransferInfo(receiverAddress, thirdAddress, 5)
            };

            // Has balance
            var stateMock = new Mock<IStateRepository>();
            stateMock.Setup(x => x.GetAccountState(contractAddress)).Returns(new AccountState
            {
                CodeHash = new byte[32],
                StateRoot = new byte[32],
                TypeName = "Mock",
                UnspentHash = new byte[32]
            });

            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns(new ContractUnspentOutput
            {
                Hash = new uint256(1),
                Nvout = 1,
                Value = 100
            });

            // No internal TX should be generated
            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transferInfos, true);
            Assert.Null(internalTransaction);

            // Ensure db not updated
            stateMock.Verify(x => x.SetUnspent(contractAddress, It.IsAny<ContractUnspentOutput>()), Times.Never);
        }
    }
}
