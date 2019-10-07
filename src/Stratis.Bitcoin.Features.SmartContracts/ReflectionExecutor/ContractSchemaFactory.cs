using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR.Loader;
using Swashbuckle.AspNetCore.Swagger;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor
{
    public class ContractSchemaFactory
    {
        public static readonly Dictionary<Type, Func<Schema>> PrimitiveTypeMap = new Dictionary<Type, Func<Schema>>
        {
            { typeof(short), () => new Schema { Type = "integer", Format = "int32" } },
            { typeof(ushort), () => new Schema { Type = "integer", Format = "int32" } },
            { typeof(int), () => new Schema { Type = "integer", Format = "int32" } },
            { typeof(uint), () => new Schema { Type = "integer", Format = "int32" } },
            { typeof(long), () => new Schema { Type = "integer", Format = "int64" } },
            { typeof(ulong), () => new Schema { Type = "integer", Format = "int64" } },
            { typeof(float), () => new Schema { Type = "number", Format = "float" } },
            { typeof(double), () => new Schema { Type = "number", Format = "double" } },
            { typeof(decimal), () => new Schema { Type = "number", Format = "double" } },
            { typeof(byte), () => new Schema { Type = "integer", Format = "int32" } },
            { typeof(sbyte), () => new Schema { Type = "integer", Format = "int32" } },
            { typeof(byte[]), () => new Schema { Type = "string", Format = "byte" } },
            { typeof(sbyte[]), () => new Schema { Type = "string", Format = "byte" } },
            { typeof(char), () => new Schema { Type = "string", Format = "char" } },
            { typeof(string), () => new Schema { Type = "string" } },
            { typeof(bool), () => new Schema { Type = "boolean" } },
            { typeof(Address), () => new Schema { Type = "string" } }
        };

        /// <summary>
        /// Maps a contract assembly to its schemas.
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public IDictionary<string, Schema> Map(IContractAssembly assembly)
        {
            return this.Map(assembly.GetPublicMethods());
        }

        /// <summary>
        /// Maps a type to its schemas.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public IDictionary<string, Schema> Map(IEnumerable<MethodInfo> methods)
        {
            return methods.Select(this.Map).ToDictionary(k => k.Title, v => v);
        }

        /// <summary>
        /// Maps a single method to a schema.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public Schema Map(MethodInfo method)
        {
            var schema = new Schema();
            schema.Properties = new Dictionary<string, Schema>();
            schema.Title = method.Name;

            foreach (var parameter in method.GetParameters())
            {
                // Default to string.
                Schema paramSchema = PrimitiveTypeMap.ContainsKey(parameter.ParameterType)
                    ? PrimitiveTypeMap[parameter.ParameterType]()
                    : PrimitiveTypeMap[typeof(string)]();

                schema.Properties.Add(parameter.Name, paramSchema);
            }

            return schema;
        }
    }
}
