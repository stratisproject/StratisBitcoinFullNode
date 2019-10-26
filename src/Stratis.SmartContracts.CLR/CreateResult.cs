namespace Stratis.SmartContracts.CLR
{
    public class CreateResult : ICreateResult
    {
        /// <inheritdoc />
        public Address NewContractAddress { get; private set; }

        /// <inheritdoc />
        public bool Success { get; private set; }

        public static CreateResult Succeeded(Address newAddress)
        {
            return new CreateResult
            {
                Success = true,
                NewContractAddress = newAddress
            };
        }

        public static CreateResult Failed()
        {
            return new CreateResult();
        }
    }
}
