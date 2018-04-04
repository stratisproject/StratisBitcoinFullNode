using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Tests
{
    /// <summary>
    /// Helper class containing a bunch of methods used for testing the wallet functionality.
    /// </summary>
    public class WalletTestsHelpers
    {
        public static GeneralPurposeAccount CreateAccount(string name)
        {
            return new GeneralPurposeAccount
            {
                Name = name,
            };
        }

        public static SpendingDetails CreateSpendingDetails(TransactionData changeTransaction, PaymentDetails paymentDetails)
        {
            var spendingDetails = new SpendingDetails
            {
                TransactionId = changeTransaction.Id,
                CreationTime = new DateTimeOffset(new DateTime(2017, 6, 23, 1, 2, 3)),
                BlockHeight = changeTransaction.BlockHeight
            };

            spendingDetails.Payments.Add(paymentDetails);
            return spendingDetails;
        }

        public static PaymentDetails CreatePaymentDetails(Money amount, GeneralPurposeAddress destinationAddress)
        {
            return new PaymentDetails
            {
                Amount = amount,
                DestinationAddress = destinationAddress.Address,
                DestinationScriptPubKey = destinationAddress.ScriptPubKey
            };
        }

        public static TransactionData CreateTransaction(uint256 id, Money amount, int? blockHeight, SpendingDetails spendingDetails = null, DateTimeOffset? creationTime = null, Script script = null)
        {
            if (creationTime == null)
            {
                creationTime = new DateTimeOffset(new DateTime(2017, 6, 23, 1, 2, 3));
            }

            return new TransactionData
            {
                Amount = amount,
                Id = id,
                CreationTime = creationTime.Value,
                BlockHeight = blockHeight,
                SpendingDetails = spendingDetails,
                ScriptPubKey = script
            };
        }

        public static GeneralPurposeAddress CreateAddress(bool changeAddress = false)
        {
            var key = new Key();
            var address = new GeneralPurposeAddress
            {
                Address = key.PubKey.GetAddress(Network.Main).ToString(),
                ScriptPubKey = key.ScriptPubKey
            };

            return address;
        }

        public static ChainedBlock AppendBlock(ChainedBlock previous = null, params ConcurrentChain[] chains)
        {
            ChainedBlock last = null;
            var nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        public static (ChainedBlock ChainedBlock, Block Block) AppendBlock(ChainedBlock previous, ConcurrentChain chain)
        {
            ChainedBlock last = null;
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();

            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
            block.Header.Nonce = nonce;
            if (!chain.TrySetTip(block.Header, out last))
                throw new InvalidOperationException("Previous not existing");

            return (last, block);
        }

        public static TransactionBuildContext CreateContext(GeneralPurposeWalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        public static GeneralPurposeWallet CreateWallet(string name)
        {
            return new GeneralPurposeWallet
            {
                Name = name,
                AccountsRoot = new List<AccountRoot>(),
                BlockLocator = null
            };
        }

        public static GeneralPurposeWallet GenerateBlankWallet(string name, string password)
        {
            return GenerateBlankWalletWithExtKey(name, password);
        }

        public static GeneralPurposeWallet GenerateBlankWalletWithExtKey(string name, string password)
        {
	        GeneralPurposeWallet walletFile = new GeneralPurposeWallet
			{
                Name = name,
                CreationTime = DateTimeOffset.Now,
                Network = Network.Main,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<GeneralPurposeAccount>(), CoinType = (CoinType)Network.Main.Consensus.CoinType } },
            };

            return walletFile;
        }

	    public static (Key PrivateKey, PubKey PubKey, BitcoinPubKeyAddress Address) GenerateAccountKeys(GeneralPurposeWallet wallet, bool changeAddress = false)
	    {
		    var account = wallet.AccountsRoot.First().Accounts.First();
		    var addressTemp = account.CreateAddresses(wallet.Network, 1, changeAddress);
		    GeneralPurposeAddress tempAddress = null;

		    if (!changeAddress)
		    {
			    foreach (var temp in account.ExternalAddresses)
				    if (temp.Address.Equals(addressTemp.First()))
					    tempAddress = temp;
		    }
		    else
		    {
			    foreach (var temp in account.InternalAddresses)
				    if (temp.Address.Equals(addressTemp.First()))
					    tempAddress = temp;
		    }

			var addressPubKey = tempAddress.PrivateKey.PubKey;
		    var address = addressPubKey.GetAddress(wallet.Network);

		    return (tempAddress.PrivateKey, addressPubKey, address);
	    }

		public static (Key PrivateKey, PubKey PubKey, BitcoinPubKeyAddress Address) GenerateAddressKeys(GeneralPurposeWallet wallet, bool changeAddress = false)
	    {
		    var account = wallet.AccountsRoot.First().Accounts.First();
		    var addressTemp = account.CreateAddresses(wallet.Network, 1, changeAddress);
		    GeneralPurposeAddress tempAddress = null;

		    if (!changeAddress)
		    {
			    foreach (var temp in account.ExternalAddresses)
				    if (temp.Address.Equals(addressTemp.First()))
					    tempAddress = temp;
		    }
		    else
		    {
			    foreach (var temp in account.InternalAddresses)
				    if (temp.Address.Equals(addressTemp.First()))
					    tempAddress = temp;
			}

			var addressPubKey = tempAddress.PrivateKey.PubKey;
		    var address = addressPubKey.GetAddress(wallet.Network);

		    return (tempAddress.PrivateKey, addressPubKey, address);
	    }

		public static Block AppendTransactionInNewBlockToChain(ConcurrentChain chain, Transaction transaction)
        {
            ChainedBlock last = null;
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(transaction);
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Tip.HashBlock;
            block.Header.Nonce = nonce;
            if (!chain.TrySetTip(block.Header, out last))
                throw new InvalidOperationException("Previous not existing");

            return block;
        }

        public static Transaction SetupValidTransaction(GeneralPurposeWallet wallet, string password, GeneralPurposeAddress spendingAddress, PubKey destinationPubKey, GeneralPurposeAddress changeAddress, Money amount, Money fee)
        {
            var spendingTransaction = spendingAddress.Transactions.ElementAt(0);
            Coin coin = new Coin(spendingTransaction.Id, (uint)spendingTransaction.Index, spendingTransaction.Amount, spendingTransaction.ScriptPubKey);

            var builder = new TransactionBuilder();
            Transaction tx = builder
                .AddCoins(new List<Coin> { coin })
                .AddKeys(spendingAddress.PrivateKey.GetBitcoinSecret(wallet.Network))
                .Send(destinationPubKey.ScriptPubKey, amount)
                .SetChange(changeAddress.ScriptPubKey)
                .SendFees(fee)
                .BuildTransaction(true);

            if (!builder.Verify(tx))
            {
                throw new GeneralPurposeWalletException("Could not build transaction, please make sure you entered the correct data.");
            }

            return tx;
        }

        public static void AddAddressesToWallet(GeneralPurposeWalletManager walletManager, int count)
        {
            foreach (var wallet in walletManager.Wallets)
            {
                wallet.AccountsRoot.Add(new AccountRoot()
                {
                    CoinType = CoinType.Bitcoin,
                    Accounts = new List<GeneralPurposeAccount>
                    {
                        new GeneralPurposeAccount
                        {
                            ExternalAddresses = GenerateAddresses(count),
                            InternalAddresses = GenerateAddresses(count)
                        },
                        new GeneralPurposeAccount
                        {
                            ExternalAddresses = GenerateAddresses(count),
                            InternalAddresses = GenerateAddresses(count)
                        } }
                });
            }
        }

        public static GeneralPurposeAddress CreateAddressWithoutTransaction(int index, string addressName)
        {
            return new GeneralPurposeAddress
            {
                Address = addressName,
                ScriptPubKey = new Script(),
                Transactions = new List<TransactionData>()
            };
        }

        public static GeneralPurposeAddress CreateAddressWithEmptyTransaction(int index, string addressName)
        {
            return new GeneralPurposeAddress
            {
                Address = addressName,
                ScriptPubKey = new Script(),
                Transactions = new List<TransactionData> { new TransactionData() }
            };
        }

        public static List<GeneralPurposeAddress> GenerateAddresses(int count)
        {
            List<GeneralPurposeAddress> addresses = new List<GeneralPurposeAddress>();
            for (int i = 0; i < count; i++)
            {
                GeneralPurposeAddress address = new GeneralPurposeAddress
                {
                    ScriptPubKey = new Key().ScriptPubKey
                };
                addresses.Add(address);
            }
            return addresses;
        }

        public static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        /// <summary>
        /// Creates a set of chains 'forking' at a specific block height. You can see the left chain as the old one and the right as the new chain.
        /// </summary>
        /// <param name="blockAmount">Amount of blocks on each chain.</param>
        /// <param name="network">The network to use</param>
        /// <param name="forkBlock">The height at which to put the fork.</param>
        /// <returns></returns>
        public static (ConcurrentChain LeftChain, ConcurrentChain RightChain, List<Block> LeftForkBlocks, List<Block> RightForkBlocks)
            GenerateForkedChainAndBlocksWithHeight(int blockAmount, Network network, int forkBlock)
        {
            var rightchain = new ConcurrentChain(network);
            var leftchain = new ConcurrentChain(network);
            var prevBlockHash = rightchain.Genesis.HashBlock;
            var leftForkBlocks = new List<Block>();
            var rightForkBlocks = new List<Block>();

            // build up left fork fully and right fork until forkblock
            uint256 forkBlockPrevHash = null;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = RandomUtils.GetUInt32();
                leftchain.SetTip(block.Header);

                if (leftchain.Height == forkBlock)
                {
                    forkBlockPrevHash = block.GetHash();
                }
                prevBlockHash = block.GetHash();
                leftForkBlocks.Add(block);

                if (rightchain.Height < forkBlock)
                {
                    rightForkBlocks.Add(block);
                    rightchain.SetTip(block.Header);
                }
            }

            // build up the right fork further.
            for (var i = forkBlock; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = forkBlockPrevHash;
                block.Header.Nonce = RandomUtils.GetUInt32();
                rightchain.SetTip(block.Header);
                forkBlockPrevHash = block.GetHash();
                rightForkBlocks.Add(block);
            }

            // if all blocks are on both sides the fork fails.
            if (leftForkBlocks.All(l => rightForkBlocks.Select(r => r.GetHash()).Contains(l.GetHash())))
            {
                throw new InvalidOperationException("No fork created.");
            }

            return (leftchain, rightchain, leftForkBlocks, rightForkBlocks);
        }

        public static (ConcurrentChain Chain, List<Block> Blocks) GenerateChainAndBlocksWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            var blocks = new List<Block>();
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
                blocks.Add(block);
            }

            return (chain, blocks);
        }

        public static ConcurrentChain PrepareChainWithBlock()
        {
            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);
            return chain;
        }

        public static ICollection<GeneralPurposeAddress> CreateSpentTransactionsOfBlockHeights(Network network, params int[] blockHeights)
        {
            var addresses = new List<GeneralPurposeAddress>();

            foreach (int height in blockHeights)
            {
                var key = new Key();
                var address = new GeneralPurposeAddress
                {
                    Address = key.PubKey.GetAddress(network).ToString(),
                    ScriptPubKey = key.ScriptPubKey,
                    Transactions = new List<TransactionData> {
                        new TransactionData
                        {
                            BlockHeight = height,
                            Amount = new Money(new Random().Next(500000, 1000000)),
                            SpendingDetails = new SpendingDetails()
                        }
                    }
                };

                addresses.Add(address);
            }

            return addresses;
        }

        public static ICollection<GeneralPurposeAddress> CreateUnspentTransactionsOfBlockHeights(Network network, params int[] blockHeights)
        {
            var addresses = new List<GeneralPurposeAddress>();

            foreach (int height in blockHeights)
            {
                var key = new Key();
                var address = new GeneralPurposeAddress
                {
                    Address = key.PubKey.GetAddress(network).ToString(),
                    ScriptPubKey = key.ScriptPubKey,
                    Transactions = new List<TransactionData> {
                        new TransactionData
                        {
                            BlockHeight = height,
                            Amount = new Money(new Random().Next(500000, 1000000))
                        }
                    }
                };

                addresses.Add(address);
            }

            return addresses;
        }

        public static TransactionData CreateTransactionDataFromFirstBlock((ConcurrentChain chain, uint256 blockHash, Block block) chainInfo)
        {
            var transaction = chainInfo.block.Transactions[0];

            var addressTransaction = new TransactionData
            {
                Amount = transaction.TotalOut,
                BlockHash = chainInfo.blockHash,
                BlockHeight = chainInfo.chain.GetBlock(chainInfo.blockHash).Height,
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(chainInfo.block.Header.Time),
                Id = transaction.GetHash(),
                Index = 0,
                ScriptPubKey = transaction.Outputs[0].ScriptPubKey,
            };
            return addressTransaction;
        }

        public static (ConcurrentChain chain, uint256 blockhash, Block block) CreateChainAndCreateFirstBlockWithPaymentToAddress(Network network, GeneralPurposeAddress address)
        {
            var chain = new ConcurrentChain(network);

            Block block = new Block();
            block.Header.HashPrevBlock = chain.Tip.HashBlock;
            block.Header.Bits = block.Header.GetWorkRequired(network, chain.Tip);
            block.Header.UpdateTime(DateTimeOffset.UtcNow, network, chain.Tip);

            var coinbase = new Transaction();
            coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
            coinbase.AddOutput(new TxOut(network.GetReward(chain.Height + 1), address.ScriptPubKey));

            block.AddTransaction(coinbase);
            block.Header.Nonce = 0;
            block.UpdateMerkleRoot();
            block.Header.CacheHashes();

            chain.SetTip(block.Header);

            return (chain, block.GetHash(), block);
        }

        public static List<Block> AddBlocksWithCoinbaseToChain(Network network, ConcurrentChain chain, GeneralPurposeAddress address, int blocks = 1)
        {
            //var chain = new ConcurrentChain(network.GetGenesis().Header);

            var blockList = new List<Block>();

            for (int i = 0; i < blocks; i++)
            {
                Block block = new Block();
                block.Header.HashPrevBlock = chain.Tip.HashBlock;
                block.Header.Bits = block.Header.GetWorkRequired(network, chain.Tip);
                block.Header.UpdateTime(DateTimeOffset.UtcNow, network, chain.Tip);

                var coinbase = new Transaction();
                coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
                coinbase.AddOutput(new TxOut(network.GetReward(chain.Height + 1), address.ScriptPubKey));

                block.AddTransaction(coinbase);
                block.Header.Nonce = 0;
                block.UpdateMerkleRoot();
                block.Header.CacheHashes();

                chain.SetTip(block.Header);

                var addressTransaction = new TransactionData
                {
                    Amount = coinbase.TotalOut,
                    BlockHash = block.GetHash(),
                    BlockHeight = chain.GetBlock(block.GetHash()).Height,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time),
                    Id = coinbase.GetHash(),
                    Index = 0,
                    ScriptPubKey = coinbase.Outputs[0].ScriptPubKey,
                };

                address.Transactions.Add(addressTransaction);

                blockList.Add(block);
            }

            return blockList;
        }
    }
}
