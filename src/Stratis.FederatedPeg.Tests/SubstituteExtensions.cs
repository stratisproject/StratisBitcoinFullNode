using System.Linq;
using System.Reflection;

namespace Stratis.FederatedPeg.Tests
{
    public static class SubstituteExtensions
    {
        public static object Protected(this object target, string name, params object[] args)
        {
            var type = target.GetType();
            var method = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(x => x.Name == name && x.IsVirtual).Single();
            return method.Invoke(target, args);
        }
    }
}