using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// An attribute that enables mapping between command names and P2P netowrk types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PayloadAttribute : Attribute
    {
        /// <summary>
        /// The command name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Initialize a new instance of the object.
        /// </summary>
        /// <param name="commandName"></param>
        public PayloadAttribute(string commandName)
        {
            this.Name = commandName;
        }
    }

    /// <summary>
    /// A provider that maps <see cref="PayloadAttribute"/> types with <see cref="Message.Command"/>.
    /// This is used by the P2P code to map and deserialize messages that are received from the tcp network to a concrete type.
    /// </summary>
    public class PayloadProvider
    {
        /// <summary>
        /// A mapping between the type and the command name.
        /// </summary>
        private readonly Dictionary<string, Type> nameToType;

        /// <summary>
        /// A mapping between the type and the command name.
        /// </summary>
        private readonly Dictionary<Type, string> typeToName;

        /// <summary>
        /// Initialize a new instance of the object.
        /// </summary>
        public PayloadProvider()
        {
            this.nameToType = new Dictionary<string, Type>();
            this.typeToName = new Dictionary<Type, string>();
        }

        /// <summary>
        /// Disvoer all payloads from the provided assembly, if no assembly is provided defaults to <see cref="PayloadAttribute"/>.
        /// </summary>
        /// <param name="assembly">the assembly to discover from or <see cref="PayloadAttribute"/> if <c>null</c>.</param>
        public PayloadProvider DiscoverPayloads(Assembly assembly = null)
        {
            assembly = assembly ?? typeof(PayloadAttribute).GetTypeInfo().Assembly;

            IEnumerable<TypeInfo> types = null;
                
            try
            {
                types = assembly.DefinedTypes;
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).Select(t => t.GetTypeInfo());
            }

            foreach (var pair in types
                .Where(t => t.Namespace == typeof(PayloadAttribute).Namespace)
                .Where(t => t.IsDefined(typeof(PayloadAttribute), true))
                .Select(t =>
                    new
                    {
                        Attr = t.GetCustomAttributes(typeof(PayloadAttribute), true).OfType<PayloadAttribute>().First(),
                        Type = t
                    }))
            {
                this.nameToType.Add(pair.Attr.Name, pair.Type.AsType());
                this.typeToName.Add(pair.Type.AsType(), pair.Attr.Name);
            }

            return this;
        }

        public Type GetCommandType(string commandName)
        {
            if (!this.nameToType.TryGetValue(commandName, out Type result))
                return typeof(UnknowPayload);

            return result;
        }

        public bool IsPayloadRegistered(Type type)
        {
            return this.typeToName.TryGetValue(type, out string result);
        }
    }
}