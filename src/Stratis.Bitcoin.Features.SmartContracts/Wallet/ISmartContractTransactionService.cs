using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public interface ISmartContractTransactionService
    {
        BuildCallContractTransactionResponse BuildCallTx(BuildCallContractTransactionRequest request);
        BuildCreateContractTransactionResponse BuildCreateTx(BuildCreateContractTransactionRequest request);
        ContractTxData BuildLocalCallTxData(LocalCallContractRequest request);

    }
}