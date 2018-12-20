using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public class WalletToUnlock
    {
        public WalletToUnlock()
        {

        }

        public WalletToUnlock(WalletAccountReference account, string password, int timeout)
        {
            this.Account = account;
            this.Password = password;
            this.Timeout = timeout;
        }

        public WalletAccountReference Account { get; set; }

        public string Password { get; set; }

        public int Timeout { get; set; }
    }
}
