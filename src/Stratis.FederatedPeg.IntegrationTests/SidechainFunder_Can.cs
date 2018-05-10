using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.GeneralPurposeWallet;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.MainchainGeneratorServices;
using Stratis.FederatedPeg.Features.SidechainGeneratorServices;
using Stratis.FederatedPeg.IntegrationTests.Helpers;
using Stratis.Sidechains.Features.BlockchainGeneration;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp;
using Xunit;
using FeeType = Stratis.Bitcoin.Features.Wallet.FeeType;
using GpRecipient = Stratis.Bitcoin.Features.GeneralPurposeWallet.Recipient;
using WtRecipient = Stratis.Bitcoin.Features.Wallet.Recipient;

using GpTransactionBuildContext = Stratis.Bitcoin.Features.GeneralPurposeWallet.TransactionBuildContext;
using WtTransactionBuildContext = Stratis.Bitcoin.Features.Wallet.TransactionBuildContext;

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.FederatedPeg.IntegrationTests
{
    // Use Case references. (See documents in the requirements folder n the repo.)
    // UCInit: Generate and Initialize Sidechain.
    // UCGenF: Generate Federation Member Key Pairs.
    // UCFund: Send Funds to Sidechain.
    public class SidechainFunder_Can
    {
        [Fact]
        public async Task deposit_funds_to_sidechain()
        {
            int addNodeDelay = 4000;

            TestUtils.ShellCleanupFolder("TestData\\deposit_funds_to_sidechain");
            TestUtils.ShellCleanupFolder("Federations\\deposit_funds_to_sidechain");

            //check empty
            Directory.Exists("TestData\\deposit_funds_to_sidechain").Should().BeFalse();
            Directory.Exists("Federations\\deposit_funds_to_sidechain").Should().BeFalse();

            // UCInit: Precondition - Generate a Blockchain using the Stratis Blockchain Generation
            //         technology. (Not shown in this test.  We have pre-prepared a chain called 'enigma').
            string sidechain_folder = @"..\..\..\..\..\assets";

            using (var nodeBuilder = NodeBuilder.Create())
            using (SidechainIdentifier.Create("enigma", sidechain_folder))
            {
                SidechainIdentifier.Instance.Name.Should().Be("enigma");

                //creates the federation folder
                var fedFolder = new TestFederationFolder();

                //
                // Act as Sidechain Generator.
                //

                // UCInit:  The actor communicates with each Federation Member and they follow the process
                //          described in the Generate Federation Member Key Pairs use case.

                //
                // Act as Federation Member(s).
                //
                //  The actor communicates with each Federation Member and they follow the process described in
                //  the Generate Federation Member Key Pairs use case.
                //  This calls the command line console to generate the keys and send their public key to
                //  the SidechainGenerator.



                // UCGenF:  The Federation Member actor navigates to an application and issues a command to
                //          Generate Federation Key Pairs.
                // UCGenF:  The actor enters their full name.
                // UCGenF:  The actor enters a Password and asked to confirm it.
                // UCGenF:  The actor is reminded to not forget or share his password.
                //          (Console app will output: "Keep this pass phrase safe.")
                // UCGenF:  The actor issues the generate command.  Text files are produced and it is made
                //          absolutely clear once again that the user is not to lose his password and to
                //          take care of the files.
                //          (Console app output: "Two of the files are PRIVATE keys that you must keep secret.
                //          Do not distribute these private keys.")

                // The RunFedKeyPair creates a folder for each member to simulate what they create locally.
                fedFolder.RunFedKeyPairGen("member1", "pass1");
                fedFolder.RunFedKeyPairGen("member2", "pass2");
                fedFolder.RunFedKeyPairGen("member3", "pass3");

                // Give file operations a chance to complete.
                await Task.Delay(2000);

                File.Exists(Path.Combine(fedFolder.Folder, "member1\\PRIVATE_DO_NOT_SHARE_Mainchain_member1.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member1\\PRIVATE_DO_NOT_SHARE_Sidechain_member1.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member1\\PUBLIC_Mainchain_member1.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member1\\PUBLIC_Sidechain_member1.txt")).Should().BeTrue();

                File.Exists(Path.Combine(fedFolder.Folder, "member2\\PRIVATE_DO_NOT_SHARE_Mainchain_member2.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member2\\PRIVATE_DO_NOT_SHARE_Sidechain_member2.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member2\\PUBLIC_Mainchain_member2.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member2\\PUBLIC_Sidechain_member2.txt")).Should().BeTrue();

                File.Exists(Path.Combine(fedFolder.Folder, "member3\\PRIVATE_DO_NOT_SHARE_Mainchain_member3.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member3\\PRIVATE_DO_NOT_SHARE_Sidechain_member3.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member3\\PUBLIC_Mainchain_member3.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member3\\PUBLIC_Sidechain_member3.txt")).Should().BeTrue();

                // UCGenF: The actor communicates the public keys with the Sidechain Generator.
                // DistributeKeys copies the public keys from each member to the parent folder in order
                // to simulate the distribution of keys to the Sidechain Generator.
                // UCInit: The actor receives all the public keys from the Federation Members.
                fedFolder.DistributeKeys(new[] {"member1", "member2", "member3"});

                File.Exists(Path.Combine(fedFolder.Folder, "PUBLIC_Mainchain_member1.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "PUBLIC_Sidechain_member1.txt")).Should().BeTrue();

                File.Exists(Path.Combine(fedFolder.Folder, "PUBLIC_Mainchain_member2.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "PUBLIC_Sidechain_member2.txt")).Should().BeTrue();

                File.Exists(Path.Combine(fedFolder.Folder, "PUBLIC_Mainchain_member3.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "PUBLIC_Sidechain_member3.txt")).Should().BeTrue();

                // Give file operations a chance to complete.
                await Task.Delay(2000);

                //
                // Act as Sidechain Generator
                //

                // UCInit:  The actor navigates to an initialize sidechain feature. He enters the multi-sig
                //          quorum parameters (eg 12 of 20) and enters the folder location (federation folder)
                //          where the collected public keys are located.

                // This part of the use case is realized on a by running a mainchain and a sidechain each with
                // added Mainchain/Sidechain Generation Services.

                // Create mainchain with MainchainGeneratorServices.
                var mainchainNode_GeneratorRole = nodeBuilder.CreateStratisPosNode(false, fullNodeBuilder =>
                {
                    // We will run 10+ nodes in this integration test.
                    // All these nodes will output to one console.
                    // We use the 'agent' to help identify nodes and
                    // their connections in the console.
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .AddMainchainGeneratorServices()
                        .UseApi()
                        .AddRPC();
                }, agent: "MainchainGeneratorRole ");
                mainchainNode_GeneratorRole.Start();

                //Create sidechain with SidechainGeneratorServices.
                var sidechainNode_GeneratorRole = nodeBuilder.CreatePosSidechainNode("enigma", false, fullNodeBuilder =>
                    {
                        fullNodeBuilder
                            .UsePosConsensus()
                            .UseBlockStore()
                            .UseMempool()
                            .UseWallet()
                            .AddPowPosMining()
                            .AddSidechainGeneratorServices()
                            .UseApi()
                            .AddRPC();
                    },
                    (n, s) => { },  //don't init a sidechain here
                    agent: "SidechainGeneratorRole "
                );

                //At the same time we create another sidechain node and connect it to the newly initialized Sidechain
                //to create a new sidechain network.
                var sidechainNode_Member1_Wallet = nodeBuilder.CreatePosSidechainNode("enigma", false, fullNodeBuilder =>
                    {
                        fullNodeBuilder
                            .UsePosConsensus()
                            .UseBlockStore()
                            .UseMempool()
                            .UseWallet()
                            .AddPowPosMining()

                            // The multi-sigs will usually be monitored on the FederationGateway.
                            // However, that is not a requirement and any node can monitor the
                            // multi-sigs.  In this case we will add the wallet to our FunderRole
                            // node and use the wallet to confirm the premine is generated into
                            // the multi-sig address.
                            .UseGeneralPurposeWallet()
                            .UseApi()
                            .AddRPC();
                    },
                    (n, s) => { }, // Don't init a sidechain here.
                    agent: "SidechainMember1Wallet "
                );

                var sidechainNode_FunderRole = nodeBuilder.CreatePosSidechainNode("enigma", false, fullNodeBuilder =>
                    {
                        fullNodeBuilder
                            .UsePosConsensus()
                            .UseBlockStore()
                            .UseMempool()
                            .UseWallet()
                            .AddPowPosMining()
                            .UseApi()
                            .AddRPC();
                    },
                    (n, s) => { }, // Don't init a sidechain here.
                    agent: "SidechainFunderRole "
                );

                // Start the engines! 
                sidechainNode_GeneratorRole.Start();
                sidechainNode_Member1_Wallet.Start();
                sidechainNode_FunderRole.Start();

                //give nodes startup time
                await Task.Delay(10000);

                // Connect the sidechain nodes together.
                var rpcClientGR = sidechainNode_GeneratorRole.CreateRPCClient();
                rpcClientGR.AddNode(sidechainNode_Member1_Wallet.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClientGR.AddNode(sidechainNode_FunderRole.Endpoint);
                await Task.Delay(addNodeDelay);

                var rpcClientMW = sidechainNode_Member1_Wallet.CreateRPCClient();
                rpcClientMW.AddNode(sidechainNode_GeneratorRole.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClientMW.AddNode(sidechainNode_FunderRole.Endpoint);
                await Task.Delay(addNodeDelay);

                var rpcClientFR = sidechainNode_FunderRole.CreateRPCClient();
                rpcClientFR.AddNode(sidechainNode_Member1_Wallet.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClientFR.AddNode(sidechainNode_GeneratorRole.Endpoint);
                await Task.Delay(addNodeDelay);

                // Let our two sidechain nodes sync together.
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechainNode_GeneratorRole, sidechainNode_Member1_Wallet));

                // Create a wallet and add our multi-sig.
                await ApiCalls.CreateGeneralPurposeWallet(sidechainNode_Member1_Wallet.ApiPort, "multisig_wallet", "password");
                var account_member1 = fedFolder.ImportPrivateKeyToWallet(sidechainNode_Member1_Wallet, "multisig_wallet", "password", "member1", "pass1", 2, 3, SidechainNetwork.SidechainRegTest);

                // UCInit:  The actor navigates to an initialize sidechain feature. He enters the multi-sig
                //          quorum parameters (eg 12 of 20) and enters the folder location (federation folder)
                //          where the collected public keys are located.
                //          The actor issues the Initialize Sidechain Command. The sidechain is initialized.
                // UCInit:  The actor receives the ScriptPubKey (redeem script) files for each Federated Member.
                //          A public address is also generated. (InitSidechain does this.)
                await ApiCalls.InitSidechain("enigma", mainchainNode_GeneratorRole.ApiPort, sidechainNode_GeneratorRole.ApiPort, 2, 3, fedFolder.Folder);

                File.Exists(Path.Combine(fedFolder.Folder, "Mainchain_Address.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "Sidechain_Address.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "Mainchain_ScriptPubKey.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "Sidechain_ScriptPubKey.txt")).Should().BeTrue();

                // Let our two sidechain nodes sync together.
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechainNode_GeneratorRole, sidechainNode_Member1_Wallet));

                // UCInit:  These files must be sent back to the federation members to store with their
                //          important sidechain key files.  There are four small files in total since a
                //          redeem script and an address are required for both sidechain and mainchain.
                // UCGenF:  The Sidechain Generator sends back two ScriptPubKeys and derived addresses
                //          that must also be stored securely with the other files.
                fedFolder.DistributeScriptAndAddress(new[] { "member1", "member2", "member3" });

                File.Exists(Path.Combine(fedFolder.Folder, "member1\\Mainchain_Address.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member1\\Sidechain_Address.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member1\\Mainchain_ScriptPubKey.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member1\\Sidechain_ScriptPubKey.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member2\\Mainchain_Address.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member2\\Sidechain_Address.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member2\\Mainchain_ScriptPubKey.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member2\\Sidechain_ScriptPubKey.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member3\\Mainchain_Address.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member3\\Sidechain_Address.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member3\\Mainchain_ScriptPubKey.txt")).Should().BeTrue();
                File.Exists(Path.Combine(fedFolder.Folder, "member3\\Sidechain_ScriptPubKey.txt")).Should().BeTrue();

                // Check we imported the multi-sig correctly.
                var memberFolderManager = fedFolder.CreateMemberFolderManager();
                account_member1.MultiSigAddresses.First().Address.Should()
                    .Be(memberFolderManager.ReadAddress(Chain.Sidechain));

                // Read the new multi-sig addresses.
                string multiSigAddress_Mainchain = memberFolderManager.ReadAddress(Chain.Mainchain);
                string multiSigAddress_Sidechain = memberFolderManager.ReadAddress(Chain.Sidechain);

                // Check we got the right balance in the multi-sig after the sidechain premine.
                var amounts = account_member1.GetSpendableAmount(true);
                amounts.ConfirmedAmount.Should().Be(new Money(98000008, MoneyUnit.BTC));

                // UCInit:   The use case ends.
                // UCGenF:   The use case ends.
                // At this stage we no longer need our Generator role nodes.
                mainchainNode_GeneratorRole.Kill();
                sidechainNode_GeneratorRole.Kill();

                //
                // Act as Sidechain Funder
                //

                // UCFund:  The actor navigates to his Sidechain wallet and issues the Receive command.
                //          The wallet displays a Sidechain Destination Address which he can copy. 

                // This step will be UI but we can simulate it with just the backend code that the UI will use.
                string sidechainWallet = "sidechain_wallet";
                string mnemonic = await ApiCalls.Mnemonic(sidechainNode_Member1_Wallet.ApiPort);
                string create_mnemonic = await ApiCalls.Create(sidechainNode_Member1_Wallet.ApiPort, mnemonic, sidechainWallet,
                    sidechainNode_Member1_Wallet.FullNode.DataFolder.WalletPath);
                create_mnemonic.Should().Be(mnemonic);
                //and gets an address. we'll use this as the sidechain destination address where he wants to send his funds
                string addressSidechain = await ApiCalls.UnusedAddress(sidechainNode_Member1_Wallet.ApiPort, sidechainWallet);

                // USFund:  The actor navigates to his Mainchain wallet and issues the command to Send Funds
                //          to Sidechain.  The actor views the name of the chain and therefore can verify
                //          that he is sending to the correct chain. The actor enters the Sidechain
                //          Destination Address that he copied in the step above.  He then also enters the
                //          Multi-Sig Federation Address that he obtained previously.

                //start mainchain
                //we are now acting as a Sidechain Funder.
                var mainchain_SidechainFunder1 = nodeBuilder.CreateStratisPosNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .UseApi()
                        .AddRPC();
                }, agent: "MainchainSidechainFunder1 ");

                //start a second mainchain
                var mainchain_SidechainFunder2 = nodeBuilder.CreateStratisPosNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .UseApi()
                        .AddRPC();
                }, agent: "MainchainSidechainFunder2 ");

                //join our nodes together and startup
                mainchain_SidechainFunder1.ConfigParameters.Add("addnode", $"127.0.0.1:{mainchain_SidechainFunder2.ProtocolPort}");
                mainchain_SidechainFunder2.ConfigParameters.Add("addnode", $"127.0.0.1:{mainchain_SidechainFunder1.ProtocolPort}");
                mainchain_SidechainFunder1.Start();
                mainchain_SidechainFunder2.Start();

                //mine some strat mainchain coins
                string mainchainWallet = "mainchain_wallet";
                mnemonic = await ApiCalls.Mnemonic(mainchain_SidechainFunder1.ApiPort);
                create_mnemonic = await ApiCalls.Create(mainchain_SidechainFunder1.ApiPort, mnemonic, mainchainWallet,
                    mainchain_SidechainFunder1.FullNode.DataFolder.WalletPath);
                create_mnemonic.Should().Be(mnemonic);
                //our source address
                string addressMainchain = await ApiCalls.UnusedAddress(mainchain_SidechainFunder1.ApiPort, mainchainWallet);

                //put some strat in the source address
                var powMinting = mainchain_SidechainFunder1.FullNode.NodeService<IPowMining>();
                var bitcoinAddress = new BitcoinPubKeyAddress(addressMainchain, Network.StratisRegTest);
                powMinting.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 50UL, int.MaxValue);

                //sync our mainchain nodes
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_SidechainFunder1, mainchain_SidechainFunder2));

                //check we have expected funds in our mainchain wallet
                var account_mainchain_funder1 = mainchain_SidechainFunder1.FullNode.WalletManager().GetAccounts("mainchain_wallet").First();
                amounts = account_mainchain_funder1.GetSpendableAmount();
                amounts.ConfirmedAmount.Should().Be(new Money(98000196, MoneyUnit.BTC));

                // Send Funds (Deposit from Mainchain to Sidechain)
                var sendingWalletAccountReference = new WalletAccountReference("mainchain_wallet", "account 0");

                var transactionBuildContext = new WtTransactionBuildContext(
                        sendingWalletAccountReference,
                        new List<WtRecipient>() { new WtRecipient() { Amount = new Money(3600, MoneyUnit.BTC), ScriptPubKey = BitcoinAddress.Create(multiSigAddress_Mainchain, Network.StratisRegTest).ScriptPubKey } },
                        "1234", addressSidechain)
                {
                    MinConfirmations = 1,
                    TransactionFee = new Money(0.001m, MoneyUnit.BTC),
                    Shuffle = true
                };

                // UCFund:  The actor issues the command to Send the transaction and the wallet 
                //          confirms and broadcasts the transaction in the normal manner.
                var transaction = mainchain_SidechainFunder1.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);
                mainchain_SidechainFunder1.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));

                await Task.Delay(5000);

                //sync our node to distrubute the mempool
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_SidechainFunder1, mainchain_SidechainFunder2));

                //generate a block to include our transaction
                //powMinting.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 1UL, int.MaxValue);

                //sync nodes
                //at this point our transaction has made its way into a block
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_SidechainFunder1, mainchain_SidechainFunder2));

                await Task.Delay(5000);

                //confirm our mainchain funder has sent some funds (less 3600 + 4 mining plus we get our fee back)
                //amounts = account_mainchain_funder1.GetSpendableAmount();
                //amounts.ConfirmedAmount.Should().Be(new Money(97996600, MoneyUnit.BTC));

                // First we'll need to mine more blocks on the multi-sig so we can spend mature funds.
                var powMinting_Sidechain = sidechainNode_Member1_Wallet.FullNode.NodeService<IPowMining>();
                bitcoinAddress = new BitcoinPubKeyAddress(addressSidechain, SidechainNetwork.SidechainRegTest);
                powMinting_Sidechain.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 50UL, int.MaxValue);

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechainNode_FunderRole, sidechainNode_Member1_Wallet));

                //
                // Act as a Federation Gateway (mainchain)
                // 
                var mainchain_FederationGateway1 = nodeBuilder.CreateStratisPosNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .AddFederationGateway()
                        .UseGeneralPurposeWallet()
                        .UseBlockNotification()
                        .UseApi()
                        .AddRPC();
                }, agent: "MainchainFederationGateway1 ");

                string publickey = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member1\\PUBLIC_mainchain_member1.txt"));
                mainchain_FederationGateway1.ConfigParameters.Add("federationfolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain"));
                mainchain_FederationGateway1.ConfigParameters.Add("memberprivatefolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member1"));
                mainchain_FederationGateway1.ConfigParameters.Add("publickey", publickey);
                mainchain_FederationGateway1.ConfigParameters.Add("membername", "member1");

                var mainchain_FederationGateway2 = nodeBuilder.CreateStratisPosNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .AddFederationGateway()
                        .UseGeneralPurposeWallet()
                        .UseBlockNotification()
                        .UseApi()
                        .AddRPC();
                }, agent: "MainchainFederationGateway2 ");

                publickey = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member2\\PUBLIC_mainchain_member2.txt"));
                mainchain_FederationGateway2.ConfigParameters.Add("federationfolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain"));
                mainchain_FederationGateway2.ConfigParameters.Add("memberprivatefolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member2"));
                mainchain_FederationGateway2.ConfigParameters.Add("publickey", publickey);
                mainchain_FederationGateway2.ConfigParameters.Add("membername", "member2");

                var mainchain_FederationGateway3 = nodeBuilder.CreateStratisPosNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .AddFederationGateway()
                        .UseGeneralPurposeWallet()
                        .UseBlockNotification()
                        .UseApi()
                        .AddRPC();
                }, agent: "MainchainFederationGateway3 ");

                publickey = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member3\\PUBLIC_mainchain_member3.txt"));
                mainchain_FederationGateway3.ConfigParameters.Add("federationfolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain"));
                mainchain_FederationGateway3.ConfigParameters.Add("memberprivatefolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member3"));
                mainchain_FederationGateway3.ConfigParameters.Add("publickey", publickey);
                mainchain_FederationGateway3.ConfigParameters.Add("membername", "member3");

                //
                // Act as a Federation Gateway (sidechain)
                // 
                var sidechain_FederationGateway1 = nodeBuilder.CreatePosSidechainNode("enigma", false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .AddFederationGateway()
                        .UseGeneralPurposeWallet()
                        .UseBlockNotification()
                        .UseApi()
                        .AddRPC();
                },
                    (n, s) => { }, //don't init a sidechain here
                    agent: "SidechainFederationGateway1 "
                );

                publickey = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member1\\PUBLIC_sidechain_member1.txt"));
                sidechain_FederationGateway1.ConfigParameters.Add("federationfolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain"));
                sidechain_FederationGateway1.ConfigParameters.Add("memberprivatefolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member1"));
                sidechain_FederationGateway1.ConfigParameters.Add("publickey", publickey);
                sidechain_FederationGateway1.ConfigParameters.Add("membername", "member1");

                var sidechain_FederationGateway2 = nodeBuilder.CreatePosSidechainNode("enigma", false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .AddFederationGateway()
                        .UseGeneralPurposeWallet()
                        .UseBlockNotification()
                        .UseApi()
                        .AddRPC();
                }, (n, s) => { }, //don't init a sidechain here
                    agent: "SidechainFederationGateway2 "
                );

                publickey = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member2\\PUBLIC_sidechain_member2.txt"));
                sidechain_FederationGateway2.ConfigParameters.Add("federationfolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain"));
                sidechain_FederationGateway2.ConfigParameters.Add("memberprivatefolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member2"));
                sidechain_FederationGateway2.ConfigParameters.Add("publickey", publickey);
                sidechain_FederationGateway2.ConfigParameters.Add("membername", "member2");

                var sidechain_FederationGateway3 = nodeBuilder.CreatePosSidechainNode("enigma", false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .AddFederationGateway()
                        .UseGeneralPurposeWallet()
                        .UseBlockNotification()
                        .UseApi()
                        .AddRPC();
                },
                    (n, s) => { }, //don't init a sidechain here
                    agent: "SidechainFederationGateway3 "
                );

                publickey = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member3\\PUBLIC_sidechain_member3.txt"));
                sidechain_FederationGateway3.ConfigParameters.Add("federationfolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain"));
                sidechain_FederationGateway3.ConfigParameters.Add("memberprivatefolder", Path.Combine(Directory.GetCurrentDirectory(), "Federations\\deposit_funds_to_sidechain\\member3"));
                sidechain_FederationGateway3.ConfigParameters.Add("publickey", publickey);
                sidechain_FederationGateway3.ConfigParameters.Add("membername", "member3");

                //link the mainchain and sidechain nodes together
                mainchain_FederationGateway1.ConfigParameters.Add("counterchainapiport", sidechain_FederationGateway1.ApiPort.ToString());
                mainchain_FederationGateway2.ConfigParameters.Add("counterchainapiport", sidechain_FederationGateway2.ApiPort.ToString());
                mainchain_FederationGateway3.ConfigParameters.Add("counterchainapiport", sidechain_FederationGateway3.ApiPort.ToString());
                sidechain_FederationGateway1.ConfigParameters.Add("counterchainapiport", mainchain_FederationGateway1.ApiPort.ToString());
                sidechain_FederationGateway2.ConfigParameters.Add("counterchainapiport", mainchain_FederationGateway2.ApiPort.ToString());
                sidechain_FederationGateway3.ConfigParameters.Add("counterchainapiport", mainchain_FederationGateway3.ApiPort.ToString());

                //start mainchain and sidechain
                mainchain_FederationGateway1.Start();
                mainchain_FederationGateway2.Start();
                mainchain_FederationGateway3.Start();

                //give nodes a chance to start
                await Task.Delay(10000);

                sidechain_FederationGateway1.Start();
                sidechain_FederationGateway2.Start();
                sidechain_FederationGateway3.Start();

                //give nodes a chance to start
                await Task.Delay(10000);

                //add mainchain nodes
                var rpcClient1 = mainchain_FederationGateway1.CreateRPCClient();
                rpcClient1.AddNode(mainchain_SidechainFunder1.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClient1.AddNode(mainchain_SidechainFunder2.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClient1.AddNode(mainchain_FederationGateway2.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClient1.AddNode(mainchain_FederationGateway3.Endpoint);
                await Task.Delay(addNodeDelay);

                var rpcClient2 = mainchain_FederationGateway2.CreateRPCClient();
                rpcClient2.AddNode(mainchain_SidechainFunder1.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClient2.AddNode(mainchain_SidechainFunder2.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClient2.AddNode(mainchain_FederationGateway1.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClient2.AddNode(mainchain_FederationGateway3.Endpoint);
                await Task.Delay(addNodeDelay);

                var rpcClient3 = mainchain_FederationGateway3.CreateRPCClient();
                rpcClient3.AddNode(mainchain_SidechainFunder1.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClient3.AddNode(mainchain_SidechainFunder2.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClient3.AddNode(mainchain_FederationGateway1.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcClient3.AddNode(mainchain_FederationGateway2.Endpoint);
                await Task.Delay(addNodeDelay);

                //add sidechain nodes
                var rpcSidechainClient1 = sidechain_FederationGateway1.CreateRPCClient();
                rpcSidechainClient1.AddNode(sidechainNode_FunderRole.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcSidechainClient1.AddNode(sidechain_FederationGateway2.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcSidechainClient1.AddNode(sidechain_FederationGateway3.Endpoint);
                await Task.Delay(addNodeDelay);

                var rpcSidechainClient2 = sidechain_FederationGateway2.CreateRPCClient();
                rpcSidechainClient2.AddNode(sidechainNode_FunderRole.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcSidechainClient2.AddNode(sidechain_FederationGateway1.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcSidechainClient2.AddNode(sidechain_FederationGateway3.Endpoint);
                await Task.Delay(addNodeDelay);

                var rpcSidechainClient3 = sidechain_FederationGateway3.CreateRPCClient();
                rpcSidechainClient3.AddNode(sidechainNode_FunderRole.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcSidechainClient3.AddNode(sidechain_FederationGateway1.Endpoint);
                await Task.Delay(addNodeDelay);
                rpcSidechainClient3.AddNode(sidechain_FederationGateway2.Endpoint);
                await Task.Delay(addNodeDelay);

                IntegrationTestUtils.AreConnected(sidechain_FederationGateway1, sidechain_FederationGateway2).Should().BeTrue();
                IntegrationTestUtils.AreConnected(sidechain_FederationGateway1, sidechain_FederationGateway3).Should().BeTrue();
                IntegrationTestUtils.AreConnected(sidechain_FederationGateway2, sidechain_FederationGateway1).Should().BeTrue();
                IntegrationTestUtils.AreConnected(sidechain_FederationGateway2, sidechain_FederationGateway3).Should().BeTrue();
                IntegrationTestUtils.AreConnected(sidechain_FederationGateway3, sidechain_FederationGateway1).Should().BeTrue();
                IntegrationTestUtils.AreConnected(sidechain_FederationGateway3, sidechain_FederationGateway2).Should().BeTrue();
                IntegrationTestUtils.AreConnected(sidechain_FederationGateway1, sidechainNode_FunderRole).Should().BeTrue();
                IntegrationTestUtils.AreConnected(sidechain_FederationGateway2, sidechainNode_FunderRole).Should().BeTrue();
                IntegrationTestUtils.AreConnected(sidechain_FederationGateway3, sidechainNode_FunderRole).Should().BeTrue();

                IntegrationTestUtils.AreConnected(mainchain_FederationGateway1, mainchain_FederationGateway2).Should().BeTrue();
                IntegrationTestUtils.AreConnected(mainchain_FederationGateway1, mainchain_FederationGateway3).Should().BeTrue();
                IntegrationTestUtils.AreConnected(mainchain_FederationGateway2, mainchain_FederationGateway1).Should().BeTrue();
                IntegrationTestUtils.AreConnected(mainchain_FederationGateway2, mainchain_FederationGateway3).Should().BeTrue();
                IntegrationTestUtils.AreConnected(mainchain_FederationGateway3, mainchain_FederationGateway1).Should().BeTrue();
                IntegrationTestUtils.AreConnected(mainchain_FederationGateway3, mainchain_FederationGateway2).Should().BeTrue();

                //create wallets on the sidechains
                //sidechain_FederationGateway1
                await ApiCalls.CreateGeneralPurposeWallet(sidechain_FederationGateway1.ApiPort, "multisig_wallet", "password");
                var account_fed_member1_sidechain = fedFolder.ImportPrivateKeyToWallet(sidechain_FederationGateway1, "multisig_wallet", "password", "member1", "pass1", 2, 3, SidechainNetwork.SidechainRegTest);

                //sidechain_FederationGateway2
                await ApiCalls.CreateGeneralPurposeWallet(sidechain_FederationGateway2.ApiPort, "multisig_wallet", "password");
                var account_fed_member2_sidechain = fedFolder.ImportPrivateKeyToWallet(sidechain_FederationGateway2, "multisig_wallet", "password", "member2", "pass2", 2, 3, SidechainNetwork.SidechainRegTest);

                //sidechain_FederationGateway3
                await ApiCalls.CreateGeneralPurposeWallet(sidechain_FederationGateway3.ApiPort, "multisig_wallet", "password");
                var account_fed_member3_sidechain = fedFolder.ImportPrivateKeyToWallet(sidechain_FederationGateway3, "multisig_wallet", "password", "member3", "pass3", 2, 3, SidechainNetwork.SidechainRegTest);

                await Task.Delay(5000);

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway1, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway2, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway3, sidechainNode_Member1_Wallet));

                IntegrationTestUtils.ResyncGeneralWallet(sidechain_FederationGateway1);
                IntegrationTestUtils.ResyncGeneralWallet(sidechain_FederationGateway2);
                IntegrationTestUtils.ResyncGeneralWallet(sidechain_FederationGateway3);

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway1, 53));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway2, 53));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway3, 53));

                bitcoinAddress = new BitcoinPubKeyAddress(addressSidechain, SidechainNetwork.SidechainRegTest);
                powMinting_Sidechain.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 1UL, int.MaxValue);
             
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway1, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway2, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway3, sidechainNode_Member1_Wallet));

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway1, 54));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway2, 54));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway3, 54));

                IntegrationTestUtils.SaveGeneralWallet(sidechain_FederationGateway1, "multisig_wallet");
                IntegrationTestUtils.SaveGeneralWallet(sidechain_FederationGateway2, "multisig_wallet");
                IntegrationTestUtils.SaveGeneralWallet(sidechain_FederationGateway3, "multisig_wallet");

                //check we have the correct balance in the multisigs
                amounts = account_fed_member1_sidechain.GetSpendableAmount(true);
                amounts.ConfirmedAmount.Should().Be(new Money(98000008, MoneyUnit.BTC));

                amounts = account_fed_member2_sidechain.GetSpendableAmount(true);
                amounts.ConfirmedAmount.Should().Be(new Money(98000008, MoneyUnit.BTC));

                amounts = account_fed_member3_sidechain.GetSpendableAmount(true);
                amounts.ConfirmedAmount.Should().Be(new Money(98000008, MoneyUnit.BTC));


                //create wallets on the mainchains
                //mainchain_FederationGateway1
                await ApiCalls.CreateGeneralPurposeWallet(mainchain_FederationGateway1.ApiPort, "multisig_wallet", "password");
                var account_fed_member1_mainchain = fedFolder.ImportPrivateKeyToWallet(mainchain_FederationGateway1, "multisig_wallet", "password", "member1", "pass1", 2, 3, Network.StratisRegTest);

                //mainchain_FederationGateway2
                await ApiCalls.CreateGeneralPurposeWallet(mainchain_FederationGateway2.ApiPort, "multisig_wallet", "password");
                var account_fed_member2_mainchain = fedFolder.ImportPrivateKeyToWallet(mainchain_FederationGateway2, "multisig_wallet", "password", "member2", "pass2", 2, 3, Network.StratisRegTest);

                //mainchain_FederationGateway3
                await ApiCalls.CreateGeneralPurposeWallet(mainchain_FederationGateway3.ApiPort, "multisig_wallet", "password");
                var account_fed_member3_mainchain = fedFolder.ImportPrivateKeyToWallet(mainchain_FederationGateway3, "multisig_wallet", "password", "member3", "pass3", 2, 3, Network.StratisRegTest);

                //generate a block to include our transaction
                bitcoinAddress = new BitcoinPubKeyAddress(addressMainchain, Network.StratisRegTest);
                powMinting.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 1UL, int.MaxValue);

                //IntegrationTestUtils.ResyncGeneralWallet(mainchain_FederationGateway1);
                //IntegrationTestUtils.ResyncGeneralWallet(mainchain_FederationGateway2);
                //IntegrationTestUtils.ResyncGeneralWallet(mainchain_FederationGateway3);

                //sync all the federation gateway nodes
                //await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_FederationGateway1, mainchain_SidechainFunder1));
                //await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_FederationGateway2, mainchain_SidechainFunder1));
                //await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_FederationGateway3, mainchain_SidechainFunder1));

                //await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(mainchain_FederationGateway1, 51));
                //await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(mainchain_FederationGateway2, 51));
                //await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(mainchain_FederationGateway3, 51));

                await Task.Delay(15000);

                //the session process occurs every 30 seconds so give it enough time to kick in.
                await Task.Delay(60000);

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway1, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway2, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway3, sidechainNode_Member1_Wallet));

                IntegrationTestUtils.ResyncGeneralWallet(sidechain_FederationGateway1);
                IntegrationTestUtils.ResyncGeneralWallet(sidechain_FederationGateway2);
                IntegrationTestUtils.ResyncGeneralWallet(sidechain_FederationGateway3);

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway1, 54));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway2, 54));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway3, 54));

                bitcoinAddress = new BitcoinPubKeyAddress(addressSidechain, SidechainNetwork.SidechainRegTest);
                powMinting_Sidechain.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 1UL, int.MaxValue);

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway1, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway2, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway3, sidechainNode_Member1_Wallet));

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway1, 55));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway2, 55));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway3, 55));

                IntegrationTestUtils.SaveGeneralWallet(sidechain_FederationGateway1, "multisig_wallet");
                IntegrationTestUtils.SaveGeneralWallet(sidechain_FederationGateway2, "multisig_wallet");
                IntegrationTestUtils.SaveGeneralWallet(sidechain_FederationGateway3, "multisig_wallet");

                //check the mainchain multi-sig has the sent funds locked. 
                amounts = account_fed_member1_mainchain.GetSpendableAmount(true);
                var confirmedAmountLockedOnMainchain = amounts.ConfirmedAmount.ToString();
                amounts.ConfirmedAmount.Should().Be(new Money(3600, MoneyUnit.BTC));

                //97996407.99000000 (98,000,008 - 3600 - 0.01 fee)
                //check the sidechain multi-sig has sent funds out of the multisig. 
                amounts = account_fed_member1_sidechain.GetSpendableAmount(true);
                var confirmedAmountMultiSigOnSidechain = amounts.ConfirmedAmount.ToString();
                amounts.ConfirmedAmount.Should().Be(new Money(98000008 - 3600 - 0.01m, MoneyUnit.BTC));

                //3804.001 (3600, 204 mining plus 0.01 transaction fee.)
                //check thos funds were received by the sidechain destination address
                var account_sidechain_funder = sidechainNode_Member1_Wallet.FullNode.WalletManager().GetAccounts("sidechain_wallet").First();
                amounts = account_sidechain_funder.GetSpendableAmount();
                amounts.ConfirmedAmount.Should().Be(new Money(3600 + 208 + 0.01m, MoneyUnit.BTC));

                // Now use the newly arrived funds to create a withdrawal transaction.

                // Withdraw Funds (Withdraw from Sidechain to Mainchain)
                sendingWalletAccountReference = new WalletAccountReference("sidechain_wallet", "account 0");

                transactionBuildContext = new WtTransactionBuildContext(
                    sendingWalletAccountReference,
                    new List<WtRecipient>() { new WtRecipient() { Amount = new Money(2500, MoneyUnit.BTC), ScriptPubKey = BitcoinAddress.Create(multiSigAddress_Sidechain, SidechainNetwork.SidechainRegTest).ScriptPubKey } },
                    "1234", addressMainchain)
                {
                    MinConfirmations = 1,
                    TransactionFee = new Money(0.001m, MoneyUnit.BTC),
                    Shuffle = true
                };
                transaction = sidechainNode_Member1_Wallet.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);
                sidechainNode_Member1_Wallet.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));

                await Task.Delay(5000);

                //sync our node to distrubute the mempool
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechainNode_FunderRole, sidechainNode_Member1_Wallet));

                //generate a block to include our transaction
                bitcoinAddress = new BitcoinPubKeyAddress(addressSidechain, SidechainNetwork.SidechainRegTest);
                powMinting_Sidechain.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 1UL, int.MaxValue);

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway1, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway2, sidechainNode_Member1_Wallet));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechain_FederationGateway3, sidechainNode_Member1_Wallet));

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway1, 56));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway2, 56));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(sidechain_FederationGateway3, 56));

                IntegrationTestUtils.SaveGeneralWallet(sidechain_FederationGateway1, "multisig_wallet");
                IntegrationTestUtils.SaveGeneralWallet(sidechain_FederationGateway2, "multisig_wallet");
                IntegrationTestUtils.SaveGeneralWallet(sidechain_FederationGateway3, "multisig_wallet");

                await Task.Delay(60000);

                //mine a block on mainchain
                bitcoinAddress = new BitcoinPubKeyAddress(addressMainchain, Network.StratisRegTest);
                powMinting.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 1UL, int.MaxValue);

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_FederationGateway1, mainchain_SidechainFunder1));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_FederationGateway2, mainchain_SidechainFunder1));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_FederationGateway3, mainchain_SidechainFunder1));

                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(mainchain_FederationGateway1, 52));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(mainchain_FederationGateway2, 52));
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.IsGeneralWalletSyncedToHeight(mainchain_FederationGateway3, 52));

                IntegrationTestUtils.SaveGeneralWallet(mainchain_FederationGateway1, "multisig_wallet");
                IntegrationTestUtils.SaveGeneralWallet(mainchain_FederationGateway2, "multisig_wallet");
                IntegrationTestUtils.SaveGeneralWallet(mainchain_FederationGateway3, "multisig_wallet");

                await Task.Delay(5000);

                //3804.001 (3600, 204 mining plus 0.01 transaction fee.)
                //check thos funds were received by the sidechain destination address
                account_sidechain_funder = sidechainNode_Member1_Wallet.FullNode.WalletManager().GetAccounts("sidechain_wallet").First();
                amounts = account_sidechain_funder.GetSpendableAmount();
                var confirmedAmountDestinationSidechainAfterWithdrawal = amounts.ConfirmedAmount.ToString();
                amounts.ConfirmedAmount.Should().Be(new Money(3812.01m - 2500, MoneyUnit.BTC));

                //97996407.99000000 (98,000,008 - 3600 - 0.01 fee)
                //check the sidechain multi-sig has locked up the withdrawing funds. 
                amounts = account_fed_member1_sidechain.GetSpendableAmount(true);
                var confirmedAmountMultiSigOnSidechain2 = amounts.ConfirmedAmount.ToString();
                amounts.ConfirmedAmount.Should().Be(new Money(97996407.99m + 2500, MoneyUnit.BTC));

                //"1099.99000000"
                //confirm the mainchain multi-sig has released the locked funds for withdrawal. 
                amounts = account_fed_member1_mainchain.GetSpendableAmount(true);
                var confirmedAmountLockedOnMainchain2 = amounts.ConfirmedAmount.ToString();
                amounts.ConfirmedAmount.Should().Be(new Money(3600 - 2500 - 0.01m, MoneyUnit.BTC));

                //97999104.01000000
                //and confirm the destination has received the withdawal
                var account_mainchain_funder = mainchain_SidechainFunder1.FullNode.WalletManager().GetAccounts("mainchain_wallet").First();
                amounts = account_mainchain_funder.GetSpendableAmount();
                var confirmedAmountDestinationMainchain = amounts.ConfirmedAmount.ToString();
                amounts.ConfirmedAmount.Should().Be(new Money(98000204 - 3600 + 2500 + 0.01m, MoneyUnit.BTC));
            }
        }
    }
}