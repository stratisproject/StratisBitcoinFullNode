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
            TestHelper.WaitLoop(() => scReceiver.CreateRPCClient().GetRawMempool().Length > 0);

            scReceiver.GenerateStratisWithMiner(2);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));
        }
    }
}