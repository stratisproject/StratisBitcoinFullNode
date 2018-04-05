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
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.FederatedPeg.Features.MainchainGeneratorServices;
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

                // Check we got the right balance in the multi-sig after the premine.
                var amounts = account_member1.GetSpendableAmount(true);
                amounts.ConfirmedAmount.Should().Be(new Money(98000008, MoneyUnit.BTC));

                // UCInit:   The use case ends.
                // UCGenF:   The use case ends.
                // At this stage we no longer need our Generator role nodes.
                mainchainNode_GeneratorRole.Kill();
                sidechainNode_GeneratorRole.Kill();

                // This test is a work in progress.
                // More coming soon.
            }
        }
    }
}