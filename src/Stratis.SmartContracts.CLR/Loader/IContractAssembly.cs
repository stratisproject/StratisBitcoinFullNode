using System;
using System.Reflection;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.Loader
{
    public interface IContractAssembly
    {
        Assembly Assembly { get; }

        Type GetType(string name);

        Type DeployedType { get; }

        bool SetObserver(Observer observer);

        Observer GetObserver();
    }
}
