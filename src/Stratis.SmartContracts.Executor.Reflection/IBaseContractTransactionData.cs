namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IBaseContractTransactionData
    {
        object[] MethodParameters { get; }

        /// <summary>The maximum amount of gas units that can spent to execute this contract.</summary>
        Gas GasLimit { get; }
    }
}