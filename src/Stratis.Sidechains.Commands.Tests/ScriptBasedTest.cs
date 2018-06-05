using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Collections.ObjectModel;
using System.Reflection;
using FluentAssertions;
using System.Linq;
using System.Runtime.CompilerServices;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp;

namespace Stratis.Sidechains.Commands.Tests
{
    public class ScriptBasedTest
    {
        public string ScriptContent { get; private set; }
        
        public IList<PSObject> RunWorkingScript(string scriptFilePath, string nodeDir = null, bool resetFolder = true, int? apiPort = null, [CallerMemberName] string caller = null)
        {
            return RunScript<PSObject>(scriptFilePath, nodeDir, resetFolder, apiPort, caller).ToList();
        }

        public IList<ErrorRecord> RunFailingScript(string scriptFilePath, string nodeDir = null, bool resetFolder = true, int? apiPort = null, [CallerMemberName] string caller = null)
        {
            return RunScript<ErrorRecord>(scriptFilePath, nodeDir, resetFolder, apiPort, caller).ToList();
        }

        private IEnumerable<T> RunScript<T>(string scriptFilePath, string nodeDir = null, bool resetFolder = true, int? apiPort = null, [CallerMemberName] string caller = null)
        {
            nodeDir = nodeDir ?? PrepareNodeFolder(caller, resetFolder);
            var expectErrors = typeof(T) == typeof(ErrorRecord);

            ScriptContent = File.ReadAllText(scriptFilePath);
            if (apiPort.HasValue) ScriptContent = ScriptContent.Replace("[@API_PORT@]", apiPort.Value.ToString());
            using (var ps = PowerShell.Create())
            {
                ps.Runspace.SessionStateProxy.PSVariable.Set(new PSVariable("StratisNodeDir", nodeDir));
                ps.AddScript(ScriptContent);
                var results = ps.Invoke();

                if (ps.HadErrors)
                    return HandleErrors<T>(expectErrors, ps.HadErrors, ps.Streams.Error.ReadAll());
                else
                    return HandleResults<T>(expectErrors, ps.HadErrors, results);
            }
        }

        private IEnumerable<T> HandleErrors<T>(bool expectErrors, bool hadErros, Collection<ErrorRecord> errors)
        {
            hadErros.Should().Be(expectErrors,
                            string.Format("otherwise it means that the Script failed to run : {0}",
                            string.Join(Environment.NewLine, errors.Select(e => e.ToString()))
                      ));
            return errors.Cast<T>();
        }

        private IEnumerable<T> HandleResults<T>(bool expectErrors, bool hadErrors, Collection<PSObject> results)
        {
            hadErrors.Should().Be(expectErrors, "Test seem to have been written to check for errors");
            return results.Cast<T>();

        }
 
        private string PrepareNodeFolder(string caller, bool resetFolder)
        {
            var path = Path.Combine(Environment.CurrentDirectory, NodeBuilder.BaseTestDataPath, caller);
            if (resetFolder)
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                Directory.CreateDirectory(path);
                File.Copy(@"..\..\..\..\..\assets\sidechains.json", Path.Combine(path, "sidechains.json"));
            }

            return path;
        }
    }
}
