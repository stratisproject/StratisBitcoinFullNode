using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    public interface IContractTransactionValidationLogic
    {
        void CheckContractTransaction(ContractTxData txData, Money suppliedBudget);
    }
}
