using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.Tests.Wallet.Common
{
    /// <summary>
    /// Helper class containing a bunch of methods used for testing the wallet functionality.
    /// </summary>
    public class WalletTestsHelpers
    {
        public static HdAccount CreateAccount(string name)
        {
            return new HdAccount
            {
                Name = name,
                HdPath = "1/2/3/4/5",
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

        public static PaymentDetails CreatePaymentDetails(Money amount, HdAddress destinationAddress)
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

        public static HdAddress CreateAddress(bool changeAddress = false)
        {
            string hdPath = "1/2/3/4/5";
            if (changeAddress)
            {
                hdPath = "1/2/3/4/1";
            }
            var key = new Key();
            var address = new HdAddress
            {
                Address = key.PubKey.GetAddress(KnownNetworks.Main).ToString(),
                HdPath = hdPath,
                ScriptPubKey = key.ScriptPubKey
            };

            return address;
        }

        public static ChainedHeader AppendBlock(Network network, ChainedHeader previous = null, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                Block block = network.CreateBlock();
                block.AddTransaction(network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        public static (ChainedHeader ChainedHeader, Block Block) AppendBlock(Network network, ChainedHeader previous, ConcurrentChain chain)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            Block block = network.CreateBlock();

            block.AddTransaction(network.CreateTransaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
            block.Header.Nonce = nonce;
            if (!chain.TrySetTip(block.Header, out last))
                throw new InvalidOperationException("Previous not existing");

            return (last, block);
        }

        public static TransactionBuildContext CreateContext(Network network, WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(network)
            {
                AccountReference = accountReference,
                MinConfirmations = minConfirmations,
                FeeType = feeType,
                WalletPassword = password,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList()
            };
        }

        public static Features.Wallet.Wallet CreateWallet(string name)
        {
            return new Features.Wallet.Wallet
            {
                Name = name,
                AccountsRoot = new List<AccountRoot>(),
                BlockLocator = null
            };
        }

        public static Features.Wallet.Wallet GenerateBlankWallet(string name, string password)
        {
            return GenerateBlankWalletWithExtKey(name, password).wallet;
        }

        public static (Features.Wallet.Wallet wallet, ExtKey key) GenerateBlankWalletWithExtKey(string name, string password)
        {
            var mnemonic = new Mnemonic("grass industry beef stereo soap employ million leader frequent salmon crumble banana");
            ExtKey extendedKey = mnemonic.DeriveExtKey(password);

            var walletFile = new Features.Wallet.Wallet
            {
                Name = name,
                EncryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, KnownNetworks.Main).ToWif(),
                ChainCode = extendedKey.ChainCode,
                CreationTime = DateTimeOffset.Now,
                Network = KnownNetworks.Main,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = (CoinType)KnownNetworks.Main.Consensus.CoinType } },
            };

            return (walletFile, extendedKey);
        }

        public static Block AppendTransactionInNewBlockToChain(ConcurrentChain chain, Transaction transaction)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            Block block = chain.Network.Consensus.ConsensusFactory.CreateBlock();
            block.AddTransaction(transaction);
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Tip.HashBlock;
            block.Header.Nonce = nonce;
            if (!chain.TrySetTip(block.Header, out last))
                throw new InvalidOperationException("Previous not existing");

            return block;
        }

        public static Transaction SetupValidTransaction(Features.Wallet.Wallet wallet, string password, HdAddress spendingAddress, PubKey destinationPubKey, HdAddress changeAddress, Money amount, Money fee)
        {
            return SetupValidTransaction(wallet, password, spendingAddress, destinationPubKey.ScriptPubKey, changeAddress, amount, fee);
        }

        public static Transaction SetupValidTransaction(Features.Wallet.Wallet wallet, string password, HdAddress spendingAddress, Script destinationScript, HdAddress changeAddress, Money amount, Money fee)
        {
            TransactionData spendingTransaction = spendingAddress.Transactions.ElementAt(0);
            var coin = new Coin(spendingTransaction.Id, (uint)spendingTransaction.Index, spendingTransaction.Amount, spendingTransaction.ScriptPubKey);

            Key privateKey = Key.Parse(wallet.EncryptedSeed, password, wallet.Network);

            var builder = new TransactionBuilder(wallet.Network);
            Transaction tx = builder
                .AddCoins(new List<Coin> { coin })
                .AddKeys(new ExtKey(privateKey, wallet.ChainCode).Derive(new KeyPath(spendingAddress.HdPath)).GetWif(wallet.Network))
                .Send(destinationScript, amount)
                .SetChange(changeAddress.ScriptPubKey)
                .SendFees(fee)
                .BuildTransaction(true);

            if (!builder.Verify(tx))
            {
                throw new WalletException("Could not build transaction, please make sure you entered the correct data.");
            }

            return tx;
        }

        public static void AddAddressesToWallet(WalletManager walletManager, int count)
        {
            foreach (Features.Wallet.Wallet wallet in walletManager.Wallets)
            {
                wallet.AccountsRoot.Add(new AccountRoot()
                {
                    CoinType = CoinType.Bitcoin,
                    Accounts = new List<HdAccount>
                    {
                        new HdAccount
                        {
                            ExternalAddresses = GenerateAddresses(count),
                            InternalAddresses = GenerateAddresses(count)
                        },
                        new HdAccount
                        {
                            ExternalAddresses = GenerateAddresses(count),
                            InternalAddresses = GenerateAddresses(count)
                        } }
                });
            }
        }

        public static HdAddress CreateAddressWithoutTransaction(int index, string addressName)
        {
            return new HdAddress
            {
                Index = index,
                Address = addressName,
                ScriptPubKey = new Script(),
                Transactions = new List<TransactionData>()
            };
        }

        public static HdAddress CreateAddressWithEmptyTransaction(int index, string addressName)
        {
            return new HdAddress
            {
                Index = index,
                Address = addressName,
                ScriptPubKey = new Script(),
                Transactions = new List<TransactionData> { new TransactionData() }
            };
        }

        public static List<HdAddress> GenerateAddresses(int count)
        {
            var addresses = new List<HdAddress>();
            for (int i = 0; i < count; i++)
            {
                var address = new HdAddress
                {
                    ScriptPubKey = new Key().ScriptPubKey
                };
                addresses.Add(address);
            }
            return addresses;
        }

        public static (ExtKey ExtKey, string ExtPubKey) GenerateAccountKeys(Features.Wallet.Wallet wallet, string password, string keyPath)
        {
            var accountExtKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, password, wallet.Network), wallet.ChainCode);
            string accountExtendedPubKey = accountExtKey.Derive(new KeyPath(keyPath)).Neuter().ToString(wallet.Network);
            return (accountExtKey, accountExtendedPubKey);
        }

        public static (PubKey PubKey, BitcoinPubKeyAddress Address) GenerateAddressKeys(Features.Wallet.Wallet wallet, string accountExtendedPubKey, string keyPath)
        {
            PubKey addressPubKey = ExtPubKey.Parse(accountExtendedPubKey).Derive(new KeyPath(keyPath)).PubKey;
            BitcoinPubKeyAddress address = addressPubKey.GetAddress(wallet.Network);

            return (addressPubKey, address);
        }

        public static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(network.CreateTransaction());
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
            uint256 prevBlockHash = rightchain.Genesis.HashBlock;
            var leftForkBlocks = new List<Block>();
            var rightForkBlocks = new List<Block>();

            // build up left fork fully and right fork until forkblock
            uint256 forkBlockPrevHash = null;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(network.CreateTransaction());
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
            for (int i = forkBlock; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(network.CreateTransaction());
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
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            var blocks = new List<Block>();
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(network.CreateTransaction());
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
            var chain = new ConcurrentChain(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            Block block = KnownNetworks.StratisMain.CreateBlock();
            block.AddTransaction(KnownNetworks.StratisMain.CreateTransaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);
            return chain;
        }

        public static ICollection<HdAddress> CreateSpentTransactionsOfBlockHeights(Network network, params int[] blockHeights)
        {
            var addresses = new List<HdAddress>();

            foreach (int height in blockHeights)
            {
                var key = new Key();
                var address = new HdAddress
                {
                    Address = key.PubKey.GetAddress(network).ToString(),
                    ScriptPubKey = key.ScriptPubKey,
                    Transactions = new List<TransactionData> {
                        new TransactionData
                        {
                            BlockHeight = height,
                            Amount = new Money(new Random().Next(500000, 1000000)),
                            SpendingDetails = new SpendingDetails(),
                            Id = new uint256()
                        }
                    }
                };

                addresses.Add(address);
            }

            return addresses;
        }

        public static ICollection<HdAddress> CreateUnspentTransactionsOfBlockHeights(Network network, params int[] blockHeights)
        {
            var addresses = new List<HdAddress>();

            foreach (int height in blockHeights)
            {
                var key = new Key();
                var address = new HdAddress
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
            Transaction transaction = chainInfo.block.Transactions[0];

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

        public static (ConcurrentChain chain, uint256 blockhash, Block block) CreateChainAndCreateFirstBlockWithPaymentToAddress(Network network, HdAddress address)
        {
            var chain = new ConcurrentChain(network);

            Block block = network.Consensus.ConsensusFactory.CreateBlock();
            block.Header.HashPrevBlock = chain.Tip.HashBlock;
            block.Header.Bits = block.Header.GetWorkRequired(network, chain.Tip);
            block.Header.UpdateTime(DateTimeOffset.UtcNow, network, chain.Tip);

            Transaction coinbase = network.CreateTransaction();
            coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
            coinbase.AddOutput(new TxOut(network.GetReward(chain.Height + 1), address.ScriptPubKey));

            block.AddTransaction(coinbase);
            block.Header.Nonce = 0;
            block.UpdateMerkleRoot();
            block.Header.PrecomputeHash();

            chain.SetTip(block.Header);

            return (chain, block.GetHash(), block);
        }

        public static List<Block> AddBlocksWithCoinbaseToChain(Network network, ConcurrentChain chain, HdAddress address, int blocks = 1)
        {
            var blockList = new List<Block>();

            for (int i = 0; i < blocks; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                block.Header.HashPrevBlock = chain.Tip.HashBlock;
                block.Header.Bits = block.Header.GetWorkRequired(network, chain.Tip);
                block.Header.UpdateTime(DateTimeOffset.UtcNow, network, chain.Tip);

                Transaction coinbase = network.CreateTransaction();
                coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
                coinbase.AddOutput(new TxOut(network.GetReward(chain.Height + 1), address.ScriptPubKey));

                block.AddTransaction(coinbase);
                block.Header.Nonce = 0;
                block.UpdateMerkleRoot();
                block.Header.PrecomputeHash();

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
