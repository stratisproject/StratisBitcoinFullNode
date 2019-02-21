using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Decompilation
{
    public class CSharpContractDecompiler
    {
        public Result<string> GetSource(byte[] bytecode)
        {
            if (bytecode == null)
                return Result.Fail<string>("Bytecode cannot be null");

            using (var memStream = new MemoryStream(bytecode))
            {
                try
                {
                    ModuleDefinition modDefinition = ModuleDefinition.ReadModule(memStream);
                    var decompiler = new CSharpDecompiler(modDefinition, new DecompilerSettings { });
                    string cSharp = decompiler.DecompileWholeModuleAsString();
                    return Result.Ok(cSharp);
                }
                catch (BadImageFormatException e)
                {
                    return Result.Fail<string>(e.Message);
                }
            }
        }
    }
}
