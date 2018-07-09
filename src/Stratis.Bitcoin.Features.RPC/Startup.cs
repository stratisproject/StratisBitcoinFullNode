using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.RPC
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IObjectModelValidator, NoObjectModelValidator>();
            services.AddMvcCore(o =>
            {
                o.ValueProviderFactories.Clear();
                o.ValueProviderFactories.Add(new RPCParametersValueProvider());
            })
                .AddJsonFormatters()
                .AddFormatterMappings();
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, RPCJsonMvcOptionsSetup>());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            RpcSettings rpcSettings)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var fullNode = serviceProvider.GetService<FullNode>();

            var authorizedAccess = new RPCAuthorization();
            string cookieStr = "__cookie__:" + new uint256(RandomUtils.GetBytes(32));
            File.WriteAllText(fullNode.DataFolder.RpcCookieFile, cookieStr);
            authorizedAccess.Authorized.Add(cookieStr);
            if (rpcSettings.RpcPassword != null)
            {
                authorizedAccess.Authorized.Add(rpcSettings.RpcUser + ":" + rpcSettings.RpcPassword);
            }
            authorizedAccess.AllowIp.AddRange(rpcSettings.AllowIp);

            MvcJsonOptions options = GetMVCOptions(serviceProvider);
            Serializer.RegisterFrontConverters(options.SerializerSettings, fullNode.Network);
            app.UseMiddleware(typeof(RPCMiddleware), authorizedAccess);
            app.UseRPC();
        }

        private static MvcJsonOptions GetMVCOptions(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IOptions<MvcJsonOptions>>().Value;
        }
    }

    internal class NoObjectModelValidator : IObjectModelValidator
    {
        public void Validate(ActionContext actionContext, ValidationStateDictionary validationState, string prefix, object model)
        {
        }
    }
}
