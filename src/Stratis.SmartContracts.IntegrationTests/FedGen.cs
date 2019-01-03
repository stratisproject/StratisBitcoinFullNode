//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Mvc;
//using NBitcoin;
//using NBitcoin.Protocol;
//using Stratis.Bitcoin;
//using Stratis.Bitcoin.Builder;
//using Stratis.Bitcoin.Configuration;
//using Stratis.Bitcoin.Features.Api;
//using Stratis.Bitcoin.Features.BlockStore;
//using Stratis.Bitcoin.Features.MemoryPool;
//using Stratis.Bitcoin.Features.RPC;
//using Stratis.Bitcoin.Features.SmartContracts;
//using Stratis.Bitcoin.Features.SmartContracts.Models;
//using Stratis.Bitcoin.Features.SmartContracts.PoA;
//using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
//using Stratis.Bitcoin.Features.SmartContracts.Wallet;
//using Stratis.Bitcoin.Features.Wallet;
//using Stratis.Bitcoin.IntegrationTests.Common;
//using Stratis.Bitcoin.Utilities;
//using Stratis.SmartContracts.CLR.Compilation;
//using Stratis.SmartContracts.Core;
//using Stratis.SmartContracts.Networks;
//using Xunit;

//namespace Stratis.SmartContracts.IntegrationTests
//{
//    public class FedGen
//    {
//        [Fact]
//        public void GenerateFedKeys()
//        {
//            const int fedKeysToGen = 7;

//            var mnemonics = new List<Mnemonic>();
//            var pubKeys = new List<PubKey>();

//            for (int i = 0; i < fedKeysToGen; i++)
//            {
//                var mnemonic = new Mnemonic(Wordlist.English, (WordCount)12);
//                mnemonics.Add(mnemonic);

//                pubKeys.Add(mnemonic.DeriveExtKey().PrivateKey.PubKey);
//            }
//        }

//        [Fact]
//        public void GetFedKeyFile()
//        {
//            ExtKey extKey = HdOperations.GetExtendedKey("bleak angle whip law broken suspect minimum enact firm bulk badge gun");
//            var privateKey = extKey.PrivateKey;

//            var ms = new MemoryStream();
//            var stream = new BitcoinStream(ms, true);
//            stream.ReadWrite(ref privateKey);

//            ms.Seek(0, SeekOrigin.Begin);
//            FileStream fileStream = File.Create("federationKey.dat");
//            ms.CopyTo(fileStream);
//            fileStream.Close();
//        }

//        [Fact]
//        public async Task StressTest()
//        {
//            NodeSettings nodeSettings = new NodeSettings(new SmartContractsPoATest(), ProtocolVersion.ALT_PROTOCOL_VERSION, "StratisSC", new string[]{"-addnode=52.189.213.175"});

//            Bitcoin.IFullNode node = new FullNodeBuilder()
//                .UseNodeSettings(nodeSettings)
//                .UseBlockStore()
//                .AddRPC()
//                .AddSmartContracts()
//                .UseSmartContractPoAConsensus()
//                .UseSmartContractPoAMining()
//                .UseSmartContractWallet()
//                .UseReflectionExecutor()
//                .UseApi()
//                .UseMempool()
//                .Build();

//            SmartContractsController controller = node.NodeService<SmartContractsController>();
//            byte[] contractCode = ContractCompiler.CompileFile("SmartContracts/Auction.cs").Compilation;

//            node.Start();
//            var timeToNodeInit = TimeSpan.FromMinutes(1);
//            var timeToNodeStart = TimeSpan.FromMinutes(1);

//            TestHelper.WaitLoop(() => node != null,
//                cancellationToken: new CancellationTokenSource(timeToNodeInit).Token,
//                failureReason: $"Failed to assign instance of FullNode within {timeToNodeInit}");

//            TestHelper.WaitLoop(() => node.State == FullNodeState.Started,
//                cancellationToken: new CancellationTokenSource(timeToNodeStart).Token,
//                failureReason: $"Failed to achieve state = started within {timeToNodeStart}");

//            while (true)
//            {
//                var request = new BuildCreateContractTransactionRequest
//                {
//                    Amount = "1",
//                    AccountName = "account 0",
//                    ContractCode = contractCode.ToHexString(),
//                    FeeAmount = "0.01",
//                    GasLimit = 100000,
//                    GasPrice = 100,
//                    Parameters = new string[] { "7#20" },
//                    Password = "password",
//                    Sender = "mvCaVED9APGFxqbxTDeaKJoiFZVySK8bNU",
//                    WalletName = "poa2"
//                };
//                JsonResult response = (JsonResult) controller.BuildAndSendCreateSmartContractTransaction(request);
//                BuildCreateContractTransactionResponse responseObject = (BuildCreateContractTransactionResponse)response.Value;
//                Assert.True(responseObject.Success);
//                Thread.Sleep(10_000);
//            }
//        }

//    }
//}
