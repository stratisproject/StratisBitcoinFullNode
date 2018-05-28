using System.Collections.Generic;
using System.Linq;

namespace Stratis.Validators.Net
{
    public sealed class ValidationResult
    {
        public List<FormatValidationError> Errors { get; private set; }

        public bool IsValid { get { return !this.Errors.Any(); } }

        public ValidationResult()
        {
            this.Errors = new List<FormatValidationError>();
        }

        public ValidationResult(List<FormatValidationError> errors)
        {
            this.Errors = errors;
        }
    }
}