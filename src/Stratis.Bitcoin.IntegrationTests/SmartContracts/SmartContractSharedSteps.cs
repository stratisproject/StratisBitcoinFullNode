using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.SmartContracts
{
    public static class SmartContractSharedSteps
    {
        public static void MineToMaturityAndSendTransaction(CoreNode scSender, CoreNode scReceiver, WalletController senderWalletController, string responseHex)
        {
            var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
            scSender.GenerateStratisWithMiner(maturity + 5);
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