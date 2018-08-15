using System;
using System.Reflection;

namespace Stratis.SmartContracts.Executor.Reflection.Loader
{
    public interface IContractAssembly
    {
        Assembly Assembly { get; }

        Type GetType(string name);
    }
}
