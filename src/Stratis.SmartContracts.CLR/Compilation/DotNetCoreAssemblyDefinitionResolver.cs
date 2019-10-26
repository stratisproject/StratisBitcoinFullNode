using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Compilation
{
    /// <summary>
    /// Resolver for .NET Core assemblies used in a 
    /// <see cref="Mono.Cecil.ModuleDefinition"/> 
    /// </summary>
    public class DotNetCoreAssemblyResolver : IAssemblyResolver
    {
        //ref: https://github.com/jbevain/cecil/issues/306

        private static readonly string BaseDirectory = System.AppContext.BaseDirectory;
        private static readonly string RuntimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);

        private readonly Dictionary<string, AssemblyDefinition> libraries;

        public DotNetCoreAssemblyResolver()
        {
            this.libraries = new Dictionary<string, AssemblyDefinition>();
        }

        // IAssemblyResolver API

        public AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            return this.Resolve(name, new ReaderParameters() { AssemblyResolver = this });
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            AssemblyDefinition def;
            if (!this.libraries.TryGetValue(name.Name, out def))
            {
                string path = Path.Combine(BaseDirectory, $"{name.Name}.dll");

                if (File.Exists(path))
                {
                    def = AssemblyDefinition.ReadAssembly(path, parameters);
                    this.libraries.Add(name.Name, def);
                }
                else
                {
                    path = Path.Combine(RuntimeDirectory, $"{name.Name}.dll");
                    if (File.Exists(path))
                    {
                        def = AssemblyDefinition.ReadAssembly(path, parameters);
                        this.libraries.Add(name.Name, def);
                    }
                    else
                    {
                        path = $"{name.Name}.dll";
                        if (File.Exists(path))
                        {
                            def = AssemblyDefinition.ReadAssembly(path, parameters);
                            this.libraries.Add(name.Name, def);
                        }
                    }
                }
            }
            return def;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }
            foreach (AssemblyDefinition def in this.libraries.Values)
            {
                def.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public static class AssemblyResolverExtensions
    {
        public static AssemblyDefinition Resolve(this IAssemblyResolver resolver, string fullName)
        {
            return resolver.Resolve(fullName, new ReaderParameters() { AssemblyResolver = resolver });
        }

        public static AssemblyDefinition Resolve(this IAssemblyResolver resolver, string fullName, ReaderParameters parameters)
        {
            if (fullName == null)
            {
                throw new ArgumentNullException(nameof(fullName));
            }
            return resolver.Resolve(AssemblyNameReference.Parse(fullName), parameters);
        }

    }
}
