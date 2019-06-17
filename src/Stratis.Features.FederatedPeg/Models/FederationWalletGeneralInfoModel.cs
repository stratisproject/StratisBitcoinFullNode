using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Features.FederatedPeg.Models
{
    public class FederationWalletGeneralInfoModel : WalletGeneralInfoModel
    {
        public string MultiSigAddress { get; set; }
    }
}
