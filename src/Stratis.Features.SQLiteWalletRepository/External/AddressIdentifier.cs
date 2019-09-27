namespace Stratis.Features.SQLiteWalletRepository.External
{
    public class AddressIdentifier
    {
        public int WalletId { get; set; }
        public int? AccountIndex { get; set; }
        public int? AddressType { get; set; }
        public int? AddressIndex { get; set; }
        public string ScriptPubKey { get; set; }

        public override bool Equals(object obj)
        {
            var address = (AddressIdentifier)obj;
            return this.WalletId == address.WalletId &&
                this.AccountIndex == address.AccountIndex &&
                this.AddressType == address.AddressType &&
                this.AddressIndex == address.AddressIndex;
        }

        public override int GetHashCode()
        {
            return (this.WalletId << 16) ^ ((this.AccountIndex ?? 0) << 14) ^ ((this.AddressType ?? 0) << 12) ^ (this.AddressIndex ?? 0);
        }
    }
}
