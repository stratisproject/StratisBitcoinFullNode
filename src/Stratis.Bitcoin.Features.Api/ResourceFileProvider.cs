using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Embedded;
using Microsoft.Extensions.Primitives;

namespace Stratis.Bitcoin.Features.Api
{
    public class ResourceFileProvider : IFileProvider, IDisposable
    {
        private readonly string rootPath;
        private readonly Assembly entryAssembly;

        public IDirectoryContents DirectoryContents { get; set; }

        public ResourceFileProvider(Assembly entryAssembly, string rootPath)
        {
            this.rootPath = rootPath;
            this.entryAssembly = entryAssembly;
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            if (subpath == null)
            {
                return new NotFoundFileInfo(subpath);
            }

            var assemblyName = this.entryAssembly.GetName().Name;
            var name = Path.GetFileName(subpath);
            var resourcePath = string.Concat(assemblyName + ".", this.rootPath, subpath.Replace("/", "."));

            if (this.entryAssembly.GetManifestResourceInfo(resourcePath) == null)
            {
                return new NotFoundFileInfo(name);
            }

            return new EmbeddedResourceFileInfo(this.entryAssembly, resourcePath, name, DateTime.UtcNow);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return null;
        }

        public IChangeToken Watch(string filter)
        {
            return NullChangeToken.Singleton;
        }

        public void Dispose()
        {
        }
    }
}