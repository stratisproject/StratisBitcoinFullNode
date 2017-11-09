using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.Protocol
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PayloadAttribute : Attribute
    {
        private static Dictionary<string, Type> nameToType;
        private static Dictionary<Type, string> typeToName;
        public string Name { get; set; }

        static PayloadAttribute()
        {
            nameToType = new Dictionary<string, Type>();
            typeToName = new Dictionary<Type, string>();

            foreach (var pair in GetLoadableTypes(typeof(PayloadAttribute).GetTypeInfo().Assembly)
                .Where(t => t.Namespace == typeof(PayloadAttribute).Namespace)
                .Where(t => t.IsDefined(typeof(PayloadAttribute), true))
                .Select(t =>
                    new
                    {
                        Attr = t.GetCustomAttributes(typeof(PayloadAttribute), true).OfType<PayloadAttribute>().First(),
                        Type = t
                    }))
            {
                nameToType.Add(pair.Attr.Name, pair.Type.AsType());
                typeToName.Add(pair.Type.AsType(), pair.Attr.Name);
            }
        }

        public PayloadAttribute(string commandName)
        {
            this.Name = commandName;
        }

        private static IEnumerable<TypeInfo> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.DefinedTypes;
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null).Select(t => t.GetTypeInfo());
            }
        }

        public static string GetCommandName<T>()
        {
            return GetCommandName(typeof(T));
        }

        public static Type GetCommandType(string commandName)
        {
            Type result;
            if (!nameToType.TryGetValue(commandName, out result))
                return typeof(UnknowPayload);

            return result;
        }

        internal static string GetCommandName(Type type)
        {
            string result;
            if (!typeToName.TryGetValue(type, out result))
                throw new ArgumentException(type.FullName + " is not a payload");

            return result;
        }
    }
}