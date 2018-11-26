using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Decompilation;

namespace Stratis.SmartContracts.Executor.Reflection.Decompilation
{
    public class CSharpContractDecompiler : IContractDecompiler
    {
        public Result<string> GetSource(byte[] bytecode)
        {
            using (var memStream = new MemoryStream(bytecode))
            {
                var modDefinition = ModuleDefinition.ReadModule(memStream);
                var decompiler = new CSharpDecompiler(modDefinition, new DecompilerSettings { });
                // TODO: Update decompiler to display all code, not just this rando FirstOrDefault (given we now allow multiple types)
                string cSharp = decompiler.DecompileAsString(modDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>"));
                return Result.Ok(cSharp);
            }
        }
    }
}
