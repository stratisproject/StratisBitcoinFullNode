using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public enum ContractTransactionItemType
    {
        Received,
        Send,
        Staked,
        ContractCall,
        ContractCreate
    }
}
