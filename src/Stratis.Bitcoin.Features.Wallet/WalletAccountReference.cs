using System;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletAccountReference
    {
        public WalletAccountReference()
        {
        }

        public WalletAccountReference(string walletName, string accountName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(accountName, nameof(accountName));

            this.WalletName = walletName;
            this.AccountName = accountName;
        }

        public string WalletName { get; set; }

        public string AccountName { get; set; }

        public override bool Equals(object obj)
        {
            var item = obj as WalletAccountReference;
            if (item == null)
                return false;
            return this.GetId().Equals(item.GetId());
        }

        public static bool operator ==(WalletAccountReference a, WalletAccountReference b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;
            return a.GetId().Equals(b.GetId());
        }

        public static bool operator !=(WalletAccountReference a, WalletAccountReference b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this.GetId().GetHashCode();
        }

        internal Tuple<string, string> GetId()
        {
            return Tuple.Create(this.WalletName, this.AccountName);
        }

        public override string ToString()
        {
            return $"{this.WalletName}:{this.AccountName}";
        }
    }
}
