//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using City.Chain.Features.SimpleWallet.Hubs;
//using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.AspNetCore.Http;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;

//namespace City.Chain.Features.SimpleWallet
//{
//    public class Startup
//    {
//        public static IServiceProvider Provider { get; private set; }

//        //public IConfiguration Configuration { get; }

//        //public Startup(IConfiguration configuration)
//        //{
//        //    this.Configuration = configuration;
//        //}

//        // This method gets called by the runtime. Use this method to add services to the container.
//        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
//        public void ConfigureServices(IServiceCollection services)
//        {
//            //services.Configure<CookiePolicyOptions>(options =>
//            //{
//            //    options.CheckConsentNeeded = context => true;
//            //    options.MinimumSameSitePolicy = SameSiteMode.None;
//            //});

//            //services.AddMvc();

//            services.AddCors(options => options.AddPolicy("CorsPolicy",
//            builder =>
//            {
//                builder.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin().AllowCredentials();
//                //.WithOrigins("https://localhost:8081");
//                //.AllowCredentials();
//            }));

//            services.AddSignalR();
//        }

//        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
//        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
//        {
//            // Only way found thus far to be able to get the IHubContext back into the context of the node. Investigate better solutions with IoC.
//            Provider = app.ApplicationServices;

//            if (env.IsDevelopment())
//            {
//                app.UseDeveloperExceptionPage();
//            }

//            ////app.UseHttpsRedirection();
//            //app.UseCookiePolicy();
//            //app.UseStaticFiles();
//            // app.UseHsts();

//            app.UseCors("CorsPolicy");

//            app.UseSignalR(routes =>
//            {
//                routes.MapHub<SimpleWalletHub>("/wallet");
//            });

//            //app.UseFileServer();
//        }
//    }
//}
