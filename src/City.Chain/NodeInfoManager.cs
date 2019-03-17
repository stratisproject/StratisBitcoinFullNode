using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Stratis.Bitcoin.Configuration;

namespace City.Chain
{
    public class NodeInfoManager
    {
        private const string FILENAME = "city.info";
        private const int DATABASE_VERSION = 120;

        private readonly NodeSettings nodeSettings;

        public NodeInfoManager(NodeSettings nodeSettings)
        {
            this.nodeSettings = nodeSettings;
        }

        public void ClearBlockchainDatabase()
        {
            // Delete blockchain database.
            var exceptions = this.TryDeleteFolders(new string[] {
                    this.nodeSettings.DataFolder.BlockPath,
                    this.nodeSettings.DataFolder.ChainPath,
                    this.nodeSettings.DataFolder.CoinViewPath,
                    //this.nodeSettings.DataFolder.FinalizedBlockInfoPath, TODO: Debug and ensure correct folders are removed.
                    this.nodeSettings.DataFolder.ProvenBlockHeaderPath
                });

            if (exceptions.Count > 0)
            {
                // Just a simple console output if one of the folders is missing, or locked.
                foreach (var exception in exceptions)
                {
                    Console.WriteLine(exception.Message);
                }
            }
        }

        public void PerformMigration(NodeInfo nodeInfo)
        {
            if (nodeInfo.DatabaseVersion < 120)
            {
                this.ClearBlockchainDatabase();

                // Write updated node info.
                nodeInfo.DatabaseVersion = 120;
                this.WriteNodeInfo(nodeInfo, this.nodeSettings);
            }
        }

        public void WriteNodeInfo(NodeInfo nodeInfo, NodeSettings nodeSettings)
        {
            var infoPath = System.IO.Path.Combine(nodeSettings.DataDir, FILENAME);
            var builder = new StringBuilder();

            builder.AppendLine("dbversion=" + nodeInfo.DatabaseVersion);

            File.WriteAllText(infoPath, builder.ToString());
        }

        /// <summary>
        /// Writes and reads the information file. Use this to verify database schema.
        /// </summary>
        /// <param name="nodeSettings"></param>
        /// <returns></returns>
        public NodeInfo CreateOrReadNodeInfo()
        {
            var nodeInfo = new NodeInfo();

            // Write the schema version, if not already exists.
            var infoPath = System.IO.Path.Combine(this.nodeSettings.DataDir, FILENAME);

            if (!File.Exists(infoPath))
            {
                // For clients earlier than this version, the database already existed so we'll
                // write that it is currently version 100.
                var infoBuilder = new System.Text.StringBuilder();

                // If the chain exists from before, but we did not have .info file, the database is old version.
                if (System.IO.Directory.Exists(this.nodeSettings.DataFolder.ChainPath))
                {
                    infoBuilder.AppendLine("dbversion=100");
                    nodeInfo.DatabaseVersion = 100;
                }
                else
                {
                    infoBuilder.AppendLine("dbversion=" + DATABASE_VERSION);
                    nodeInfo.DatabaseVersion = DATABASE_VERSION;
                }

                File.WriteAllText(infoPath, infoBuilder.ToString());
            }
            else
            {
                var fileConfig = new TextFileConfiguration(File.ReadAllText(infoPath));
                nodeInfo.DatabaseVersion = fileConfig.GetOrDefault<int>("dbversion", DATABASE_VERSION);
            }

            return nodeInfo;
        }

        private List<Exception> TryDeleteFolders(string[] paths)
        {
            var list = new List<Exception>();

            foreach (var path in paths)
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch (Exception ex)
                {
                    list.Add(ex);
                }

            }

            return list;
        }
    }
}
