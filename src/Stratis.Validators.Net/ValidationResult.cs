using Mono.Cecil;

namespace Stratis.Validators.Net
{
    public class ValidationResult
    {
        public string MethodName { get; }

        public string MethodFullName { get; }

        public string Message { get; }

        public string ValidationType { get; }

        public ValidationResult(string message)
        {
            this.Message = message;
        }

        public ValidationResult(MethodDefinition methodDefinition, string validationType, string message)
            : this(message)
        {
            this.MethodFullName = methodDefinition.FullName;
            this.MethodName = methodDefinition.Name;
            this.ValidationType = validationType;
        }

        public ValidationResult(string methodName, string methodFullName, string validationType, string message)
            : this(message)
        {
            this.MethodName = methodName;
            this.MethodFullName = methodFullName;
            this.ValidationType = validationType;
        }

        public override string ToString()
        {
            return this.Message;
        }
    }
}