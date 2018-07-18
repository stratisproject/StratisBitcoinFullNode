using System;

namespace Stratis.SmartContracts
{
    public interface ICreateResult
    {
        Address NewContractAddress { get; }

        Exception ThrownException { get; }

        bool Success { get; }
    }
}