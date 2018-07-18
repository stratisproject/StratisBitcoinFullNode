using System;

namespace Stratis.SmartContracts
{
    public interface ICreateResult
    {
        /// <summary>
        /// Address of the contract just created.
        /// </summary>
        Address NewContractAddress { get; }

        /// <summary>
        /// The exception thrown by execution, if there was one.
        /// </summary>
        Exception ThrownException { get; }

        /// <summary>
        /// Whether the constructor code ran successfully and thus whether the contract was successfully deployed.
        /// </summary>
        bool Success { get; }
    }
}