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
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.MainchainGeneratorServices;
using Stratis.FederatedPeg.Features.MainchainRuntime;
using Stratis.FederatedPeg.Features.MainchainRuntime.Models;
using Stratis.FederatedPeg.Features.SidechainGeneratorServices;
using Stratis.FederatedPeg.IntegrationTests.Helpers;
using Stratis.Sidechains.Features.BlockchainGeneration;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp;
using Xunit;

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
            // UCInit: Precondition - Generate a Blockchain using the Stratis Blockchain Generation
            //         technology. (Not shown in this test.  We have pre-prepared a chain called 'enigma').
            string sidechain_folder = @"..\..\..\..\..\assets";

            using (var nodeBuilder = NodeBuilder.Create())
            using (SidechainIdentifier.Create("enigma", sidechain_folder))
            {
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

                // UCGenF: The actor communicates the public keys with the Sidechain Generator.
                // DistributeKeys copies the public keys from each member to the parent folder in order
                // to simulate the distribution of keys to the Sidechain Generator.
                // UCInit: The actor receives all the public keys from the Federation Members.
                fedFolder.DistributeKeys(new[] {"member1", "member2", "member3"});

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
                    agent: "SidechainFunderRole "
                );

                // Connect the sidechain nodes together.
                sidechainNode_GeneratorRole.ConfigParameters.Add("addnode", $"127.0.0.1:{sidechainNode_Member1_Wallet.ProtocolPort}");
                sidechainNode_Member1_Wallet.ConfigParameters.Add("addnode", $"127.0.0.1:{sidechainNode_GeneratorRole.ProtocolPort}");

                // Start the engines! 
                sidechainNode_GeneratorRole.Start();
                sidechainNode_Member1_Wallet.Start();

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

                // Let our two sidechain nodes sync together.
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(sidechainNode_GeneratorRole, sidechainNode_Member1_Wallet));

                // UCInit:  These files must be sent back to the federation members to store with their
                //          important sidechain key files.  There are four small files in total since a
                //          redeem script and an address are required for both sidechain and mainchain.
                // UCGenF:  The Sidechain Generator sends back two ScriptPubKeys and derived addresses
                //          that must also be stored securely with the other files.
                fedFolder.DistributeScriptAndAddress(new[] { "member1", "member2", "member3" });

                // Check we imported the multi-sig correctly.
                var memberFolderManager = fedFolder.CreateMemberFolderManager();
                account_member1.MultiSigAddresses.First().Address.Should()
                    .Be(memberFolderManager.ReadAddress(Chain.Sidechain));

                // Read the new multi-sig addresses.
                string multiSigAddress_Mainchain = memberFolderManager.ReadAddress(Chain.Mainchain);
                string multiSigAddress_Sidechain = memberFolderManager.ReadAddress(Chain.Sidechain);

                // Check we got the right balance in the multi-sig after the premine.
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
                        .AddMainchainRuntime()
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
                        .AddMainchainRuntime()
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

                #region Experimental Code
                // The following code is experimental while we are waiting for a general OP_RETURN feature
                // to be added to the library.

                //send funds
                //this construct extends the normal functionality of the BuildTransaction wallet method to add an OP_RETURN
                //with our extra data <sidechain name>|<sidechain addess>
                var sendFundsToSidechainRequest = new SendFundsToSidechainRequest
                {
                    AccountName = "account 0",
                    AllowUnconfirmed = false,
                    Amount = "3600",
                    DestinationAddress = multiSigAddress_Mainchain,
                    FeeAmount = "0.001",
                    FeeType = "low",
                    Password = "1234",
                    ShuffleOutputs = true,
                    WalletName = "mainchain_wallet",

                    SidechainDestinationAddress = addressSidechain,
                    SidechainName = "enigma"
                };
                var walletBuildTransactionModel = await ApiCalls
                    .BuildTransaction(mainchain_SidechainFunder1.ApiPort, sendFundsToSidechainRequest).ConfigureAwait(false);

                // UCFund:  The actor issues the command to Send the transaction and the wallet 
                //          confirms and broadcasts the transaction in the normal manner.

                //this is currently hacked and only broadcasts the transaction. it does not add our transaction to the wallet.
                var sendTransactionRequest = new SendTransactionRequest
                {
                    Hex = walletBuildTransactionModel.Hex
                };
                await ApiCalls.SendTransaction(mainchain_SidechainFunder1.ApiPort, sendTransactionRequest);

                await Task.Delay(5000);

                //sync our node to distrubute the mempool
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_SidechainFunder1, mainchain_SidechainFunder2));

                //generate a block to include our transaction
                powMinting.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 1UL, int.MaxValue);

                //sync nodes
                //at this point our transaction has made its way into a block
                await IntegrationTestUtils.WaitLoop(() => IntegrationTestUtils.AreNodesSynced(mainchain_SidechainFunder1, mainchain_SidechainFunder2));

                await Task.Delay(5000);

                #endregion Experimental Code

                // This test is a work in progress.
                // More coming soon.
            }
        }
    }
}