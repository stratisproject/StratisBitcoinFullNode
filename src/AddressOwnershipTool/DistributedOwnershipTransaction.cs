namespace AddressOwnershipTool
{
    public class DistributedOwnershipTransaction
    {
        public string SourceAddress { get; set; }

        public string StraxAddress { get; set; }

        public decimal SenderAmount { get; set; }

        public bool TransactionBuilt { get; set; }

        public bool TransactionSent { get; set; }

        public string TransactionSentHash { get; set; }

        public DistributedOwnershipTransaction() { }

        public DistributedOwnershipTransaction(OwnershipTransaction ownershipTransaction)
        {
            this.StraxAddress = ownershipTransaction.StraxAddress;
            this.SenderAmount = ownershipTransaction.SenderAmount;
            this.SourceAddress = ownershipTransaction.SignedAddress;
        }
    }
}
