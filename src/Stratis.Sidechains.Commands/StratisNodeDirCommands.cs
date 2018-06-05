using System;
using System.IO;
using System.Management.Automation;

namespace Stratis.Sidechains.Commands
{
    /// <summary>
    /// <para type="synopsis">StratisNodeDir is the FullNode home data folder.</para>
    /// <para type="description">The data folder is set by default to special folder AppData\StratisNode and can be changed here.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "StratisNodeDir")]
    public class SetStratisNodeDirCommand : PSCmdlet
    {
        /// <summary>
        /// <para type="description">The quantity of the gizmo.</para>
        /// </summary>
        /// TODO: this won't work for linux
        [Parameter(Mandatory = true, Position = 1)]
        public string DataDir { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StratisNode");

        protected override void ProcessRecord()
        {
            try
            {
                if (this.DataDir == string.Empty)
                    this.DataDir = (string)this.SessionState.PSVariable.GetValue("StratisNodeDir");
                else
                    this.SessionState.PSVariable.Set("StratisNodeDir", this.DataDir);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new ArgumentException("StratisNodeDir must be set.");
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">StratisNodeDir is the FullNode home data folder.</para>
    /// <para type="description">The data folder is set by default to special folder AppData\StratisNode and can be changed here.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "StratisNodeDir")]
    public class GetStratisNodeDirCommand : PSCmdlet
    {
        public string DataDir { get; set; }

        protected override void ProcessRecord()
        {
            this.DataDir = this.SessionState.PSVariable.GetValue("StratisNodeDir") as string;
            if (string.IsNullOrEmpty(this.DataDir))
                this.DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StratisNode");
            WriteObject(this.DataDir);
        }
    }
}