using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Script = NBitcoin.Script;

namespace Stratis.Bitcoin.Tests.Wallet
{
    /// <summary>
    /// Helper class containing a bunch of methods used for testing the wallet functionality.
    /// </summary>
    public class WalletTestsHelpers
    {
        internal static HdAccount CreateAccount(string name)
        {
            return new HdAccount()
            {
                Name = name,
                HdPath = "1/2/3/4/5",
            };
        }

        internal static SpendingDetails CreateSpendingDetails(TransactionData changeTransaction, PaymentDetails paymentDetails)
        {
            var spendingDetails = new SpendingDetails()
            {
                TransactionId = changeTransaction.Id,
                CreationTime = new DateTimeOffset(new DateTime(2017, 6, 23, 1, 2, 3)),
                BlockHeight = changeTransaction.BlockHeight
            };

            spendingDetails.Payments.Add(paymentDetails);
            return spendingDetails;
        }

        internal static PaymentDetails CreatePaymentDetails(Money amount, HdAddress destinationAddress)
        {
            return new PaymentDetails()
            {
                Amount = amount,
                DestinationAddress = destinationAddress.Address,
                DestinationScriptPubKey = destinationAddress.ScriptPubKey
            };
        }

        internal static TransactionData CreateTransaction(uint256 id, Money amount, int? blockHeight, SpendingDetails spendingDetails = null, DateTimeOffset? creationTime = null, Script script = null)
        {
            if (creationTime == null)
            {
                creationTime = new DateTimeOffset(new DateTime(2017, 6, 23, 1, 2, 3));
            }

            return new TransactionData()
            {                
                Amount = amount,
                Id = id,
                CreationTime = creationTime.Value,
                BlockHeight = blockHeight,
                SpendingDetails = spendingDetails,
                ScriptPubKey = script
            };
        }

        internal static HdAddress CreateAddress(bool changeAddress = false)
        {
            var hdPath = "1/2/3/4/5";
            if (changeAddress)
            {
                hdPath = "1/2/3/4/1";
            }
            var key = new Key();
            var address = new HdAddress()
            {
                Address = key.PubKey.GetAddress(Network.Main).ToString(),
                HdPath = hdPath,
                ScriptPubKey = key.ScriptPubKey
            };

            return address;
        }

        internal static ChainedBlock AppendBlock(ChainedBlock previous = null, params ConcurrentChain[] chains)
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
        
        internal static (ChainedBlock ChainedBlock, Block Block) AppendBlock(ChainedBlock previous, ConcurrentChain chain)
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

        internal static TransactionBuildContext CreateContext(WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        internal static Features.Wallet.Wallet CreateWallet(string name)
        {
            return new Features.Wallet.Wallet()
            {
                Name = name,
                AccountsRoot = new List<AccountRoot>(),
                BlockLocator = null
            };
        }

        internal static Features.Wallet.Wallet GenerateBlankWallet(string name, string password)
        {
            return GenerateBlankWalletWithExtKey(name, password).wallet;
        }

        internal static (Features.Wallet.Wallet wallet, ExtKey key) GenerateBlankWalletWithExtKey(string name, string password)
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            ExtKey extendedKey = mnemonic.DeriveExtKey(password);

            Features.Wallet.Wallet walletFile = new Features.Wallet.Wallet
            {
                Name = name,
                EncryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main).ToWif(),
                ChainCode = extendedKey.ChainCode,
                CreationTime = DateTimeOffset.Now,
                Network = Network.Main,
                AccountsRoot = new List<AccountRoot> { new AccountRoot { Accounts = new List<HdAccount>(), CoinType = (CoinType)Network.Main.Consensus.CoinType } },
            };

            return (walletFile, extendedKey);
        }

        internal static Block AppendTransactionInNewBlockToChain(ConcurrentChain chain, Transaction transaction)
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

        internal static Transaction SetupValidTransaction(Features.Wallet.Wallet wallet, string password, HdAddress spendingAddress, PubKey destinationPubKey, HdAddress changeAddress, Money amount, Money fee)
        {
            var spendingTransaction = spendingAddress.Transactions.ElementAt(0);
            Coin coin = new Coin(spendingTransaction.Id, (uint)spendingTransaction.Index, spendingTransaction.Amount, spendingTransaction.ScriptPubKey);

            var privateKey = Key.Parse(wallet.EncryptedSeed, password, wallet.Network);

            var builder = new TransactionBuilder();
            Transaction tx = builder
                .AddCoins(new List<Coin> { coin })
                .AddKeys(new ExtKey(privateKey, wallet.ChainCode).Derive(new KeyPath(spendingAddress.HdPath)).GetWif(wallet.Network))
                .Send(destinationPubKey.ScriptPubKey, amount)
                .SetChange(changeAddress.ScriptPubKey)
                .SendFees(fee)
                .BuildTransaction(true);

            if (!builder.Verify(tx))
            {
                throw new WalletException("Could not build transaction, please make sure you entered the correct data.");
            }

            return tx;
        }
        
        internal static void AddAddressesToWallet(WalletManager walletManager, int count)
        {
            foreach (var wallet in walletManager.Wallets)
            {
                wallet.AccountsRoot.Add(new AccountRoot
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

        internal static HdAddress CreateAddressWithoutTransaction(int index, string addressName)
        {
            return new HdAddress()
            {
                Index = index,
                Address = addressName,
                ScriptPubKey = new Script(),
                Transactions = new List<TransactionData>()
            };
        }

        internal static HdAddress CreateAddressWithEmptyTransaction(int index, string addressName)
        {
            return new HdAddress()
            {
                Index = index,
                Address = addressName,
                ScriptPubKey = new Script(),
                Transactions = new List<TransactionData>() { new TransactionData() }
            };
        }

        internal static List<HdAddress> GenerateAddresses(int count)
        {
            List<HdAddress> addresses = new List<HdAddress>();
            for (int i = 0; i < count; i++)
            {

                HdAddress address = new HdAddress
                {
                    ScriptPubKey = new Key().ScriptPubKey
                };
                addresses.Add(address);
            }
            return addresses;
        }

        internal static (ExtKey ExtKey, string ExtPubKey) GenerateAccountKeys(Features.Wallet.Wallet wallet, string password, string keyPath)
        {
            var accountExtKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, password, wallet.Network), wallet.ChainCode);
            var accountExtendedPubKey = accountExtKey.Derive(new KeyPath(keyPath)).Neuter().ToString(wallet.Network);
            return (accountExtKey, accountExtendedPubKey);
        }

        internal static (PubKey PubKey, BitcoinPubKeyAddress Address) GenerateAddressKeys(Features.Wallet.Wallet wallet, string accountExtendedPubKey, string keyPath)
        {
            var addressPubKey = ExtPubKey.Parse(accountExtendedPubKey).Derive(new KeyPath(keyPath)).PubKey;
            var address = addressPubKey.GetAddress(wallet.Network);

            return (addressPubKey, address);
        }

        internal static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network)
        {
            var chain = new ConcurrentChain(network);
            var nonce = RandomUtils.GetUInt32();
            var prevBlockHash = chain.Genesis.HashBlock;
            for (var i = 0; i < blockAmount; i++)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        internal static ConcurrentChain PrepareChainWithBlock()
        {
            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);
            return chain;
        }

        internal static ICollection<HdAddress> CreateSpentTransactionsOfBlockHeights(Network network, params int[] blockHeights)
        {
            var addresses = new List<HdAddress>();

            foreach (int height in blockHeights)
            {
                var key = new Key();
                var address = new HdAddress()
                {
                    Address = key.PubKey.GetAddress(network).ToString(),
                    ScriptPubKey = key.ScriptPubKey,
                    Transactions = new List<TransactionData>() {
                        new TransactionData()
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

        internal static ICollection<HdAddress> CreateUnspentTransactionsOfBlockHeights(Network network, params int[] blockHeights)
        {
            var addresses = new List<HdAddress>();

            foreach (int height in blockHeights)
            {
                var key = new Key();
                var address = new HdAddress()
                {
                    Address = key.PubKey.GetAddress(network).ToString(),
                    ScriptPubKey = key.ScriptPubKey,
                    Transactions = new List<TransactionData>() {
                        new TransactionData()
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
        
        internal static TransactionData CreateTransactionDataFromFirstBlock((ConcurrentChain chain, uint256 blockHash, Block block) chainInfo)
        {
            var transaction = chainInfo.block.Transactions[0];

            var addressTransaction = new TransactionData()
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

        internal static (ConcurrentChain chain, uint256 blockhash, Block block) CreateChainAndCreateFirstBlockWithPaymentToAddress(Network network, HdAddress address)
        {
            var chain = new ConcurrentChain(network.GetGenesis().Header);

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

        internal static List<Block> AddBlocksWithCoinbaseToChain(Network network, ConcurrentChain chain, HdAddress address, int blocks = 1)
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

                var addressTransaction = new TransactionData()
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
