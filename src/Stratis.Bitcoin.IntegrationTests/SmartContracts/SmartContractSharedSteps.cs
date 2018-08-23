using System;
using System.Threading;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.SmartContracts
{
    public static class SmartContractSharedSteps
    {
        public static void SendTransactionAndMine(CoreNode scSender, CoreNode scReceiver, SmartContractWalletController senderWalletController, string responseHex)
        {
            senderWalletController.SendTransaction(new SendTransactionRequest
            {
                Hex = responseHex
            });

            TimeSpan duration = TimeSpan.FromSeconds(180);

            TestHelper.WaitLoop(() => scReceiver.CreateRPCClient().GetRawMempool().Length > 0,
                cancellationToken: new CancellationTokenSource(duration).Token,
                failureReason: $"Failed to retrive Mempool size > 0 whihin {duration}");

            scReceiver.GenerateStratisWithMiner(2);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));
        }
    }
}