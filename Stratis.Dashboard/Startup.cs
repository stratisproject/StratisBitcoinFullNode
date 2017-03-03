using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Stratis.Dashboard.Infrastructure;
using System.Text;
using System.Collections;
using System.IO;

namespace Stratis.Dashboard {
   public class Startup {
      public bool IsDevelopment { get; set; }
      public bool UseEmbeddedResources { get; set; }

      public Startup(IHostingEnvironment env) {
         var builder = new ConfigurationBuilder()
             .SetBasePath(env.ContentRootPath)
             //.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
             //.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
             .AddEnvironmentVariables();
         Configuration = builder.Build();

#if DEBUG
         this.IsDevelopment = true;
#else
         this.IsDevelopment = env.IsDevelopment();
#endif

         UseEmbeddedResources = !IsDevelopment;
      }

      public IConfigurationRoot Configuration { get; }

      // This method gets called by the runtime. Use this method to add services to the container.
      public void ConfigureServices(IServiceCollection services) {
         // Add framework services.
         var iMvcBuilder = services.AddMvc();

         if (UseEmbeddedResources) {
            var embeddedProvider = new EmbeddedFileProvider(this.GetType().GetTypeInfo().Assembly);

            //using embeddedProvider to load files (all embedded into the wallet dll)
            services.Configure<RazorViewEngineOptions>(options => {
               options.FileProviders.Clear();
               options.FileProviders.Add(embeddedProvider);
            });
         }
      }

      // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
      public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory) {
         loggerFactory.AddConsole(Configuration.GetSection("Logging"));
         loggerFactory.AddDebug();

         if (UseEmbeddedResources) {
            app.UseStaticFiles(new StaticFileOptions {
               FileProvider = new EmbeddedFileProvider(
            assembly: this.GetType().GetTypeInfo().Assembly,
            baseNamespace: "Stratis.Dashboard.wwwroot")
            });
         }
         else {
            app.UseStaticFiles();
         }

         if (IsDevelopment) {
            app.UseDeveloperExceptionPage();
         }
         else {
            app.UseExceptionHandler("/Home/Error");
         }

         app.UseMvc(routes => {
            routes.MapRoute(
                name: "default",
                template: "{controller=Home}/{action=Index}/{id?}");
         });
      }
   }










   /// <summary>
   /// Looks up files using embedded resources in the specified assembly.
   /// This file provider is case sensitive.
   /// </summary>
   public class EmbeddedFileProvider : IFileProvider {
      private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars()
          .Where(c => c != '/' && c != '\\').ToArray();

      private readonly Assembly _assembly;
      private readonly string _baseNamespace;
      private readonly DateTimeOffset _lastModified;

      /// <summary>
      /// Initializes a new instance of the <see cref="EmbeddedFileProvider" /> class using the specified
      /// assembly and empty base namespace.
      /// </summary>
      /// <param name="assembly">The assembly that contains the embedded resources.</param>
      public EmbeddedFileProvider(Assembly assembly)
          : this(assembly, assembly?.GetName()?.Name) {
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="EmbeddedFileProvider" /> class using the specified
      /// assembly and base namespace.
      /// </summary>
      /// <param name="assembly">The assembly that contains the embedded resources.</param>
      /// <param name="baseNamespace">The base namespace that contains the embedded resources.</param>
      public EmbeddedFileProvider(Assembly assembly, string baseNamespace) {
         if (assembly == null) {
            throw new ArgumentNullException("assembly");
         }

         _baseNamespace = string.IsNullOrEmpty(baseNamespace) ? string.Empty : baseNamespace + ".";
         _assembly = assembly;

         _lastModified = DateTimeOffset.UtcNow;


         // need to keep netstandard1.0 until ASP.NET Core 2.0 because it is a breaking change if we remove it
#if NETSTANDARD1_5 || NET451
            if (!string.IsNullOrEmpty(_assembly.Location))
            {
                try
                {
                    _lastModified = File.GetLastWriteTimeUtc(_assembly.Location);
                }
                catch (PathTooLongException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
#endif
      }

      /// <summary>
      /// Locates a file at the given path.
      /// </summary>
      /// <param name="subpath">The path that identifies the file. </param>
      /// <returns>
      /// The file information. Caller must check Exists property. A <see cref="NotFoundFileInfo" /> if the file could
      /// not be found.
      /// </returns>
      public IFileInfo GetFileInfo(string subpath) {
         if (string.IsNullOrEmpty(subpath)) {
            return new NotFoundFileInfo(subpath);
         }

         var builder = new StringBuilder(_baseNamespace.Length + subpath.Length);
         builder.Append(_baseNamespace);

         // Relative paths starting with a leading slash okay
         if (subpath.StartsWith("/", StringComparison.Ordinal)) {
            builder.Append(subpath, 1, subpath.Length - 1);
         }
         else {
            builder.Append(subpath);
         }

         for (var i = _baseNamespace.Length; i < builder.Length; i++) {
            if (builder[i] == '/' || builder[i] == '\\') {
               builder[i] = '.';
            }
         }

         var resourcePath = builder.ToString();
         if (HasInvalidPathChars(resourcePath)) {
            return new NotFoundFileInfo(resourcePath);
         }

         var name = System.IO.Path.GetFileName(subpath);
         if (_assembly.GetManifestResourceInfo(resourcePath) == null) {
            return new NotFoundFileInfo(name);
         }

         return new EmbeddedResourceFileInfo(_assembly, resourcePath, name, _lastModified);
      }

      /// <summary>
      /// Enumerate a directory at the given path, if any.
      /// This file provider uses a flat directory structure. Everything under the base namespace is considered to be one
      /// directory.
      /// </summary>
      /// <param name="subpath">The path that identifies the directory</param>
      /// <returns>
      /// Contents of the directory. Caller must check Exists property. A <see cref="NotFoundDirectoryContents" /> if no
      /// resources were found that match <paramref name="subpath" />
      /// </returns>
      public IDirectoryContents GetDirectoryContents(string subpath) {
         // The file name is assumed to be the remainder of the resource name.
         if (subpath == null) {
            return NotFoundDirectoryContents.Singleton;
         }

         // Relative paths starting with a leading slash okay
         if (subpath.StartsWith("/", StringComparison.Ordinal)) {
            subpath = subpath.Substring(1);
         }

         // Non-hierarchal.
         if (!subpath.Equals(string.Empty)) {
            return NotFoundDirectoryContents.Singleton;
         }

         var entries = new List<IFileInfo>();

         // TODO: The list of resources in an assembly isn't going to change. Consider caching.
         var resources = _assembly.GetManifestResourceNames();
         for (var i = 0; i < resources.Length; i++) {
            var resourceName = resources[i];
            if (resourceName.StartsWith(_baseNamespace)) {
               entries.Add(new EmbeddedResourceFileInfo(
                   _assembly,
                   resourceName,
                   resourceName.Substring(_baseNamespace.Length),
                   _lastModified));
            }
         }

         return new EnumerableDirectoryContents(entries);
      }

      /// <summary>
      /// Embedded files do not change.
      /// </summary>
      /// <param name="pattern">This parameter is ignored</param>
      /// <returns>A <see cref="NullChangeToken" /></returns>
      public Microsoft.Extensions.Primitives.IChangeToken Watch(string pattern) {
         return NullChangeToken.Singleton;
      }

      private static bool HasInvalidPathChars(string path) {
         return path.IndexOfAny(_invalidFileNameChars) != -1;
      }
   }


   internal class EnumerableDirectoryContents : IDirectoryContents {
      private readonly IEnumerable<IFileInfo> _entries;

      public EnumerableDirectoryContents(IEnumerable<IFileInfo> entries) {
         if (entries == null) {
            throw new ArgumentNullException(nameof(entries));
         }

         _entries = entries;
      }

      public bool Exists {
         get { return true; }
      }

      public IEnumerator<IFileInfo> GetEnumerator() {
         return _entries.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
         return _entries.GetEnumerator();
      }
   }

   /// <summary>
   /// Represents a file embedded in an assembly.
   /// </summary>
   public class EmbeddedResourceFileInfo : IFileInfo {
      private readonly Assembly _assembly;
      private readonly string _resourcePath;

      private long? _length;

      /// <summary>
      /// Initializes a new instance of <see cref="EmbeddedFileProvider"/> for an assembly using <paramref name="resourcePath"/> as the base
      /// </summary>
      /// <param name="assembly">The assembly that contains the embedded resource</param>
      /// <param name="resourcePath">The path to the embedded resource</param>
      /// <param name="name">An arbitrary name for this instance</param>
      /// <param name="lastModified">The <see cref="DateTimeOffset" /> to use for <see cref="LastModified" /></param>
      public EmbeddedResourceFileInfo(
          Assembly assembly,
          string resourcePath,
          string name,
          DateTimeOffset lastModified) {
         _assembly = assembly;
         _resourcePath = resourcePath;
         Name = name;
         LastModified = lastModified;
      }

      /// <summary>
      /// Always true.
      /// </summary>
      public bool Exists => true;

      /// <summary>
      /// The length, in bytes, of the embedded resource
      /// </summary>
      public long Length {
         get {
            if (!_length.HasValue) {
               using (var stream = _assembly.GetManifestResourceStream(_resourcePath)) {
                  _length = stream.Length;
               }
            }
            return _length.Value;
         }
      }

      /// <summary>
      /// Always null.
      /// </summary>
      public string PhysicalPath => null;

      /// <summary>
      /// The name of embedded file
      /// </summary>
      public string Name { get; }

      /// <summary>
      /// The time, in UTC, when the <see cref="EmbeddedFileProvider"/> was created
      /// </summary>
      public DateTimeOffset LastModified { get; }

      /// <summary>
      /// Always false.
      /// </summary>
      public bool IsDirectory => false;

      /// <inheritdoc />
      public System.IO.Stream CreateReadStream() {
         var stream = _assembly.GetManifestResourceStream(_resourcePath);
         if (!_length.HasValue) {
            _length = stream.Length;
         }
         return stream;
      }
   }
}
