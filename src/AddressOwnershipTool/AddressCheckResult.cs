namespace AddressOwnershipTool
{
    public class AddressCheckResult
    {
        public AddressCheckResult(bool hasBalance, bool hasActivity)
        {
            this.HasActivity = hasActivity;
            this.HasBalance = hasBalance;
        }

        public bool HasBalance { get; set; }

        public bool HasActivity { get; set; }
    }
}
