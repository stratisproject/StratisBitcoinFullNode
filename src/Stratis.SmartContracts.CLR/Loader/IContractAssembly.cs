using System;
using System.Collections.Generic;
using System.Reflection;

namespace Stratis.SmartContracts.CLR.Loader
{
    public interface IContractAssembly
    {
        Assembly Assembly { get; }

        Type GetType(string name);

        Type GetDeployedType();

        IEnumerable<MethodInfo> GetPublicMethods();
    }
}
