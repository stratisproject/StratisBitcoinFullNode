using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.SmartContracts;
using System.Collections.Generic;

namespace $safeprojectname$
{
    [TestClass]
    public class AuctionTests
    {
        /*
         * These tests demonstrate testing of the core logic of the contract.
         * 
         * Of course, you will want to test your contracts with the resource tracking code injected,
         * and executing on-chain, as they would be in a production environment. We are working on this
         * and will provide access to such tools when they're ready.
         */

        private static readonly Address CoinbaseAddress = (Address)"mxKorCkWmtrPoekfWiMzERJPhaT13nnkMy";
        private static readonly Address ContractOwnerAddress = (Address)"muXxezY249vn18Ho67qLnybEwzwp4t5Cwj";
        private static readonly Address ContractAddress = (Address)"muQuwkjrhCC26mTRJW7BivGBNAdZt25M1E";

        private const ulong ContractDeployBlockNumber = 1;
        private const ulong Duration = 20u;
        private const ulong GasLimit = 10000;

        private Dictionary<Address, ulong> BlockchainBalances;

        private TestSmartContractState SmartContractState;

        [TestInitialize]
        public void Initialize()
        {
            // Runs before each test
            BlockchainBalances = new Dictionary<Address, ulong>();
            var block = new TestBlock
            {
                Coinbase = CoinbaseAddress,
                Number = ContractDeployBlockNumber
            };
            var message = new TestMessage
            {
                ContractAddress = ContractAddress,
                GasLimit = (Gas)GasLimit,
                Sender = ContractOwnerAddress,
                Value = 0u
            };
            var getContractBalance = new Func<ulong>(() => BlockchainBalances[ContractAddress]);
            var persistentState = new TestPersistentState();
            var internalTransactionExecutor = new TestInternalTransactionExecutor(BlockchainBalances, ContractAddress);
            var gasMeter = new TestGasMeter((Gas)GasLimit);
            var hashHelper = new TestInternalHashHelper();

            this.SmartContractState = new TestSmartContractState(
                block,
                message,
                persistentState,
                gasMeter,
                internalTransactionExecutor,
                getContractBalance,
                hashHelper
            );
        }

        [TestMethod]
        public void TestConstruction()
        {
            var auction = new Auction(SmartContractState, Duration);

            Assert.AreEqual(ContractOwnerAddress, SmartContractState.PersistentState.GetAddress("Owner"));
            Assert.IsFalse(SmartContractState.PersistentState.GetBool("HasEnded"));
            Assert.AreEqual(Duration + SmartContractState.Block.Number, SmartContractState.PersistentState.GetUInt64("EndBlock"));
        }

        [TestMethod]
        public void TestBidding()
        {
            var auction = new Auction(SmartContractState, Duration);

            Assert.IsNull(SmartContractState.PersistentState.GetAddress("HighestBidder").Value);
            Assert.AreEqual(0uL, SmartContractState.PersistentState.GetUInt64("HighestBid"));

            ((TestMessage)SmartContractState.Message).Value = 100;

            auction.Bid();

            Assert.IsNotNull(SmartContractState.PersistentState.GetAddress("HighestBidder").Value);
            Assert.AreEqual(100uL, SmartContractState.PersistentState.GetUInt64("HighestBid"));

            ((TestMessage)SmartContractState.Message).Value = 90;

            Assert.ThrowsException<Exception>(() => auction.Bid());
        }

        [TestMethod]
        public void TestScenario_TwoBidders_VerifyBalances()
        {
            // Setup
            var auction = new Auction(SmartContractState, Duration);

            var bidderA = (Address)"bidderAAddress";
            var bidderB = (Address)"bidderBAddress";

            BlockchainBalances[ContractAddress] = 0;
            BlockchainBalances[ContractOwnerAddress] = 0;
            BlockchainBalances[bidderA] = 13;
            BlockchainBalances[bidderB] = 13;

            ulong currentSimulatedBlockNumber = ContractDeployBlockNumber;

            // Bidder A bids 10, is highest (and only) bid
            currentSimulatedBlockNumber++;
            SetBlock(currentSimulatedBlockNumber);
            MockContractMethodCall(sender: bidderA, value: 10u);
            auction.Bid();
            // Bidder B bids 11, is new highest bid
            currentSimulatedBlockNumber++;
            SetBlock(currentSimulatedBlockNumber);
            MockContractMethodCall(sender: bidderB, value: 11u);
            auction.Bid();
            // Bidder A withdraws failed bid
            currentSimulatedBlockNumber++;
            SetBlock(currentSimulatedBlockNumber);
            MockContractMethodCall(sender: bidderA, value: 0u);
            auction.Withdraw();
            // Bidder A bids 12, is new highest bid
            currentSimulatedBlockNumber++;
            SetBlock(currentSimulatedBlockNumber);
            MockContractMethodCall(sender: bidderA, value: 12u);
            auction.Bid();
            // AuctionEnd called by contract owner after end block has passed
            currentSimulatedBlockNumber = currentSimulatedBlockNumber + Duration;
            SetBlock(currentSimulatedBlockNumber);
            MockContractMethodCall(sender: ContractOwnerAddress, value: 0u);
            auction.AuctionEnd();

            // Verify end balances
            var expectedBlockchainBalances = new Dictionary<Address, ulong> {
                        { ContractAddress, 11 },
                        { ContractOwnerAddress, 12 },
                        { bidderA, 1 },
                        { bidderB, 2 }
                    };

            var expectedReturnBalances = new Dictionary<Address, ulong> {
                        { bidderA, 0 },
                        { bidderB, 11 }
                    };

            foreach (var k in expectedBlockchainBalances.Keys)
            {
                Assert.IsTrue(BlockchainBalances[k] == expectedBlockchainBalances[k]);
            }

            foreach (var k in expectedReturnBalances.Keys)
            {
                Assert.IsTrue(auction.ReturnBalances[k] == expectedReturnBalances[k]);
            }

            // Sanity check
            Assert.AreEqual(SumDictionary(BlockchainBalances), SumDictionary(expectedBlockchainBalances));
        }

        private ulong SumDictionary(Dictionary<Address, ulong> balances)
        {
            ulong sum = 0u;
            foreach (var k in balances.Keys)
            {
                sum = sum + balances[k];
            }
            return sum;
        }

        private void SetBlock(ulong blockNumber)
        {
            Assert.IsTrue(SmartContractState.Block.Number <= blockNumber); //call is sequential, otherwise invalid call
            ((TestBlock)SmartContractState.Block).Number = blockNumber;
        }

        private void MockContractMethodCall(Address sender, uint value)
        {
            Assert.IsTrue(BlockchainBalances.ContainsKey(sender)); //sender was assigned a balance at start of this test
            Assert.IsTrue(BlockchainBalances.ContainsKey(ContractAddress)); //smartcontract address was assigned a balance at start of this test
            Assert.IsTrue(BlockchainBalances[sender] >= value); //sender address has enough money for this call

            ((TestMessage)SmartContractState.Message).Sender = sender;
            ((TestMessage)SmartContractState.Message).Value = value;
            BlockchainBalances[sender] = BlockchainBalances[sender] - value;
            BlockchainBalances[ContractAddress] = BlockchainBalances[ContractAddress] + value;
        }
    }
}
