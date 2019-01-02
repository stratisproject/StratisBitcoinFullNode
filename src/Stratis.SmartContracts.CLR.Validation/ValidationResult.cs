using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    public class MethodDefinitionValidationResult : ValidationResult 
    {
        public MethodDefinitionValidationResult(string message) 
            : base(message)
        {
        }

        public MethodDefinitionValidationResult(MethodDefinition methodDefinition, string validationType, string message)
        : base(methodDefinition.Name,  validationType, message)
        {
        }

        public MethodDefinitionValidationResult(string subjectName, string validationType, string message) 
            : base(subjectName, validationType, message)
        {
        }
    }

    public class TypeDefinitionValidationResult : ValidationResult
    {
        public TypeDefinitionValidationResult(string message) 
            : base(message)
        {
        }

        public TypeDefinitionValidationResult(string subjectName, string validationType, string message) 
            : base(subjectName, validationType, message)
        {
        }
    }

    public class ModuleDefinitionValidationResult : ValidationResult
    {
        public ModuleDefinitionValidationResult(string message) 
            : base(message)
        {
        }

        public ModuleDefinitionValidationResult(string subjectName, string validationType, string message) 
            : base(subjectName, validationType, message)
        {
        }
    }

    public abstract class ValidationResult
    {
        public string SubjectName { get; }

        public string Message { get; }

        public string ValidationType { get; }

        protected ValidationResult(string message)
        {
            this.Message = message;
        }

        protected ValidationResult(string subjectName, string validationType, string message)
            : this(message)
        {
            this.SubjectName = subjectName;
            this.ValidationType = validationType;
        }

        public override string ToString()
        {
            return this.Message;
        }
    }
}