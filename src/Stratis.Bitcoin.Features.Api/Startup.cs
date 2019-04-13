using System;
using System.IO;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Swashbuckle.AspNetCore.Swagger;

namespace Stratis.Bitcoin.Features.Api
{
    public class Startup
    {
        private const string consolidatedXmlFilename = "Stratis.Bitcoin.Api.xml";
        
        public Startup(IHostingEnvironment env)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            this.Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add service and create Policy to allow Cross-Origin Requests
            services.AddCors
            (
                options =>
                {
                    options.AddPolicy
                    (
                        "CorsPolicy",

                        builder =>
                        {
                            var allowedDomains = new[] { "http://localhost", "http://localhost:4200" };

                            builder
                            .WithOrigins(allowedDomains)
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials();
                        }
                    );
                });

            // Add framework services.
            services.AddMvc(options =>
                {
                    options.Filters.Add(typeof(LoggingActionFilter));

                    ServiceProvider serviceProvider = services.BuildServiceProvider();
                    var apiSettings = (ApiSettings)serviceProvider.GetRequiredService(typeof(ApiSettings));
                    if (apiSettings.KeepaliveTimer != null)
                    {
                        options.Filters.Add(typeof(KeepaliveActionFilter));
                    }
                })
                // add serializers for NBitcoin objects
                .AddJsonOptions(options => Utilities.JsonConverters.Serializer.RegisterFrontConverters(options.SerializerSettings))
                .AddControllers(services);

            // Register the Swagger generator, defining one or more Swagger documents
            services.AddSwaggerGen(setup =>
            {
                setup.SwaggerDoc("v1", new Info { Title = "Stratis.Bitcoin.Api", Version = "v1" });

                //Set the comments path for the swagger json and ui.
                string basePath = PlatformServices.Default.Application.ApplicationBasePath;

                BuildConsolidatedXmlFile(basePath);
                
                string apiXmlPath = Path.Combine(basePath, consolidatedXmlFilename);
                string walletXmlPath = Path.Combine(basePath, "Stratis.Bitcoin.LightWallet.xml");

                if (File.Exists(apiXmlPath))
                {
                    setup.IncludeXmlComments(apiXmlPath);
                }

                if (File.Exists(walletXmlPath))
                {
                    setup.IncludeXmlComments(walletXmlPath);
                }

                setup.DescribeAllEnumsAsStrings();
            });
        }

	    // Individual C# projects can output their documentation in an XML format, which can be picked up and then
	    // displayed by Swagger. However, this presents a problem in multi-project solutions, which generate
	    // multiple XML files. The following four functions consolidate XML produced for the Full Node projects
	    // containing documentation relevant for the Swagger API.
	    //
	    // Usefully, building the Full Node solution will result in the project XML files also being produced
	    // in the binary folder of any project that references them. Each time a daemon that uses the API feature
	    // runs, the code below consolidates XML files (from projects the daemon references) into a single file.
	    //
	    // Projects by default do not produce XML documentation, and the option must be explicitly set in the project options.
	    //
	    // If you find a project with documentation you need to see in the Swagger API, make the change in the project
	    // options, and the documentation will appear in the Swagger API.
        private void BuildConsolidatedXmlFile(string basePath)
        {
            DirectoryInfo xmlDir;
            
            try
            {
                xmlDir = new DirectoryInfo(basePath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occurred: {0}", e.ToString());
                return;
            }
	
            Console.WriteLine("Searching for XML files created by Full Node projects...");
            FileInfo[] xmlFiles = xmlDir.GetFiles("*.xml");
            foreach (FileInfo file in xmlFiles)
            {
                Console.WriteLine("\tFound " + file.Name);
            }
	
            // Note: No need to delete any existing instance of the consolidated Xml file as the
            // XML writer overwrites it anyway.
	
            XmlWriter consolidatedXmlWriter = BeginConsildatedXmlFile(basePath + "/" + consolidatedXmlFilename);
            if (consolidatedXmlWriter != null)
            {
                Console.WriteLine("Consolidating XML files created by Full Node projects...");
                if (ReadAndAddMemberElementsFromGeneratedXml(basePath, xmlFiles, consolidatedXmlWriter))
                {
                    FinalizeConsolidatedXmlFile(consolidatedXmlWriter);
                }
            }
        }
        
        private XmlWriter BeginConsildatedXmlFile(string consolidatedXmlFileFullPath)
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Indent = true;
			settings.OmitXmlDeclaration = false;
			try
			{
				XmlWriter consolidatedXmlWriter = XmlWriter.Create(consolidatedXmlFileFullPath, settings);

				consolidatedXmlWriter.WriteStartElement("doc");
				consolidatedXmlWriter.WriteStartElement("assembly");
				consolidatedXmlWriter.WriteStartElement("name");
				consolidatedXmlWriter.WriteString("Stratis.Bitcoin");
				consolidatedXmlWriter.WriteEndElement();
				consolidatedXmlWriter.WriteEndElement();
				consolidatedXmlWriter.WriteStartElement("members");

				return consolidatedXmlWriter;
			}
			catch (Exception e)
			{
				Console.WriteLine("Exception occurred: {0}", e.ToString());
				return null;
			}
		}
	
		private bool ReadAndAddMemberElementsFromGeneratedXml(string xmlDirPath, FileInfo[] generatedXmlFiles, XmlWriter consolidatedXmlWriter)
		{            
			foreach (FileInfo file in generatedXmlFiles)
			{
				XmlReaderSettings settings = new XmlReaderSettings();
				string xmlFileFullPath = xmlDirPath + "/" + file.Name;
				try
				{
					XmlReader reader = XmlReader.Create(xmlFileFullPath, settings);
					bool alreadyInPosition = false; 

					reader.MoveToContent(); // positions the XML reader at the doc element.

					while (alreadyInPosition || reader.Read()) // if not in position, read the next node.
					{
						alreadyInPosition = false;
						if (reader.NodeType == XmlNodeType.Element && reader.Name == "member")
						{
							consolidatedXmlWriter.WriteNode(reader, false); 
							// Calling WriteNode() moves the position to the next member element,
							// which is exactly what is required.
							alreadyInPosition = true;  
						}
					}
					Console.WriteLine("\tConsolidated " + file.Name);
				}
				catch (Exception e)
				{
					Console.WriteLine("Exception occurred: {0}", e.ToString());
					return false;
				}
			}

			return true;
		}
	
		private void FinalizeConsolidatedXmlFile(XmlWriter consolidatedXmlWriter)
		{
			consolidatedXmlWriter.WriteEndElement();
			consolidatedXmlWriter.WriteEndElement();
			consolidatedXmlWriter.Close();

			Console.WriteLine(consolidatedXmlFilename + " finalized and ready for use!");
		}      

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(this.Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseCors("CorsPolicy");

            app.UseMvc();

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS etc.), specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.DefaultModelRendering(ModelRendering.Model);
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Stratis.Bitcoin.Api V1");
            });
        }
    }
}