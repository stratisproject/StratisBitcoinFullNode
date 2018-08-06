using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.Bitcoin.Utilities;

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
            if (commandName.Length > 12)
                throw new ArgumentException("Protocol violation: command name is limited to 12 characters.");

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
        /// A mapping between the command name and the payload type.
        /// </summary>
        private readonly Dictionary<string, Type> nameToType;

        /// <summary>
        /// A mapping between the payload type and the command name.
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
        /// Discover all payloads from the provided assembly, if no assembly is provided defaults to <see cref="PayloadAttribute"/>.
        /// </summary>
        /// <param name="assembly">The assembly to discover from or <see cref="PayloadAttribute"/> if <c>null</c>.</param>
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

        /// <summary>
        /// Add a payload to the Provider by specifying its type.
        /// </summary>
        /// <param name="type">The type to payload to add.  Must derive from <see cref="Payload"/>.</param>
        public void AddPayload(Type payloadType)
        {
            Guard.NotNull(payloadType, nameof(payloadType));
            Guard.Assert(payloadType.IsSubclassOf(typeof(Payload)));

            PayloadAttribute payloadAttribute = payloadType.GetCustomAttributes(typeof(PayloadAttribute), true)
                .OfType<PayloadAttribute>().First();
            Guard.Assert(payloadAttribute != null);

            this.nameToType.Add(payloadAttribute.Name, payloadType);
            this.typeToName.Add(payloadType, payloadAttribute.Name);
        }

        /// <summary>
        /// Get the <see cref="Payload"/> type associated with the command name.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <returns>The type of payload the command is associated with.</returns>
        public Type GetCommandType(string commandName)
        {
            if (!this.nameToType.TryGetValue(commandName, out Type result))
                return typeof(UnknowPayload);

            return result;
        }

        /// <summary>
        /// Check that a <see cref="Payload"/> type is allowed to be used in the P2P code.
        /// </summary>
        /// <param name="type">A type that represents a <see cref="Payload"/></param>
        /// <returns>True if the type is registered as a usable payload.</returns>
        public bool IsPayloadRegistered(Type type)
        {
            return this.typeToName.ContainsKey(type);
        }
    }
}
