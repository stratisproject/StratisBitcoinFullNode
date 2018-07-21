using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    public interface IReferencedMethodResolver
    {
        IEnumerable<MethodDefinition> GetReferencedMethods(MethodDefinition methodDefinition);
    }
}