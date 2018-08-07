namespace Stratis.SmartContracts
{
    public interface ICreateResult
    {
        /// <summary>
        /// Address of the contract just created.
        /// </summary>
        Address NewContractAddress { get; }

        /// <summary>
        /// Whether the constructor code ran successfully and thus whether the contract was successfully deployed.
        /// </summary>
        bool Success { get; }
    }
}