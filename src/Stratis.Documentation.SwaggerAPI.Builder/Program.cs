using System;
using System.IO;
using System.Xml;
using Microsoft.Extensions.PlatformAbstractions;

// Individual C# projects can output their documentation in an XML format, which can be picked up by and
// displayed by Swagger. However, this presents a problem in multi-project solutions, which generate
// multiple XML files. This console app consolidates the XML produced for the Full Node projects
// containing documentation relevant for the Swagger API.
//
// Simply building the Full Node solution will result in the project XML files being produced in the XML subfolder
// found inside this project's folder. Running the project will consolidate these XML files into a single file
// held in the ConsolidatedXML subfolder.
//
// Projects by default do not produce XML documentation, and the option must be explicitly set in the project options.
// Furthermore, you should add a relative path before the XML filename so the file is generated in this
// project's XML folder. Using the Stratis.Bitcoin project as an example:
//
// ../../../../Stratis.Documentation.SwaggerAPI.Builder/XML/Stratis.Bitcoin.xml
//
// If you find a project with documentation you need to see in the Swagger API, make the change in the project options.
// After the Full Node solution is rebuilt and this console app is run, you should see the documentation appear in the
// Swagger API.
//
// Note: The Swagger API by default expects the consolidated XML file to be in the ConsolidatedXML subfolder. A
// config option is available to specify an alternative relative path to the consolidated XML file.

namespace Stratis.Documentation.SwaggerAPI.Builder
{
    class Program
    {
        private const string ConsolidatedXmlFilename = "Stratis.Bitcoin.Api.xml";
        private const string RelativeXmlDirPath = "../../../XML";
        private const string RelativeConsolidatedXmlDirPath = "../../../ConsolidatedXml";

        static void Main(string[] args)
        {
            string basePath = PlatformServices.Default.Application.ApplicationBasePath;
            string xmlDirPath = Path.Combine(basePath, RelativeXmlDirPath);
            
            string consolidatedXmlFile = Path.Combine(basePath, RelativeConsolidatedXmlDirPath + "/" + ConsolidatedXmlFilename);

            DirectoryInfo xmlDir;
            try
            {
                xmlDir = new DirectoryInfo(xmlDirPath);
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

            XmlWriter consolidatedXmlWriter = BeginConsildatedXmlFile(consolidatedXmlFile);
            if (consolidatedXmlWriter != null)
            {
                Console.WriteLine("Consolidating XML files created by Full Node projects...");
                if (ReadAndAddMemberElementsFromGeneratedXml(xmlDirPath, xmlFiles, consolidatedXmlWriter))
                {
                    FinalizeConsolidatedXmlFile(consolidatedXmlWriter);
                }
            }
        }

        private static XmlWriter BeginConsildatedXmlFile(string consolidatedXmlDirPath)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.OmitXmlDeclaration = false;
            try
            {
                XmlWriter consolidatedXmlWriter = XmlWriter.Create(consolidatedXmlDirPath, settings);
    
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

        private static bool ReadAndAddMemberElementsFromGeneratedXml(string xmlDirPath, FileInfo[] generatedXmlFiles, XmlWriter consolidatedXmlWriter)
        {            
            foreach (FileInfo file in generatedXmlFiles)
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                string xmlFileFullPath = xmlDirPath + "/" + file.Name;
                try
                {
                    XmlReader reader = XmlReader.Create(xmlFileFullPath, settings); //** error check
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

        private static void FinalizeConsolidatedXmlFile(XmlWriter consolidatedXmlWriter)
        {
            consolidatedXmlWriter.WriteEndElement();
            consolidatedXmlWriter.WriteEndElement();
            consolidatedXmlWriter.Close();
            
            Console.WriteLine(ConsolidatedXmlFilename + " finalized and ready for use!");
        }
    }
}
