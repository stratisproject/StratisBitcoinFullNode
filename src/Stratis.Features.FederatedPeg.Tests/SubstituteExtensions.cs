using System;
using System.Linq;
using System.Reflection;

namespace Stratis.Features.FederatedPeg.Tests
{
    public static class SubstituteExtensions
    {
        public static object Protected(this object target, string name, params object[] args)
        {
            Type type = target.GetType();
            MethodInfo method = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(x => x.Name == name && x.IsVirtual).Single();
            return method.Invoke(target, args);
        }
    }
}