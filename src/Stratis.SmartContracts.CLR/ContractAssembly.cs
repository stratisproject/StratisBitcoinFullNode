using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stratis.SmartContracts.CLR
{
    public class ContractAssembly
    {
        private readonly Assembly assembly;

        public ContractAssembly(byte[] code)
            : this(Assembly.Load(code))
        {
        }

        public ContractAssembly(Assembly assembly)
        {
            this.assembly = assembly;
        }

        public IEnumerable<MethodInfo> GetPublicMethods()
        {
            Type deployedType = this.assembly.ExportedTypes.FirstOrDefault(t => t.GetCustomAttribute<DeployAttribute>() != null) ?? this.assembly.ExportedTypes.FirstOrDefault();

            if (deployedType == null)
            {
                return Enumerable.Empty<MethodInfo>();
            }

            return deployedType.GetMethods();
        }
    }
}
