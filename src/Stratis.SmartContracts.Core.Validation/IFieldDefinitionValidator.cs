﻿using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.Validation
{
    public interface IFieldDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(FieldDefinition field);
    }
}