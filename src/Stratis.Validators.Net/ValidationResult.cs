using Mono.Cecil;

namespace Stratis.Validators.Net
{
    public class ValidationResult
    {
        public string MethodName { get; set; }

        public string MethodFullName { get; set; }

        public string Message { get; set; }

        public string ErrorType { get; set; }

        public ValidationResult(string message)
        {
            this.Message = message;
        }

        public ValidationResult(MethodDefinition methodDefinition, string errorType, string message)
            : this(message)
        {
            this.MethodFullName = methodDefinition.FullName;
            this.MethodName = methodDefinition.Name;
            this.ErrorType = errorType;
        }

        public ValidationResult(string methodName, string methodFullName, string errorType, string message)
            : this(message)
        {
            this.MethodName = methodName;
            this.MethodFullName = methodFullName;
            this.ErrorType = errorType;
        }

        public override string ToString()
        {
            return this.Message;
        }
    }
}