using System.Collections.Generic;
using Stratis.SmartContracts.CLR.Validation;

namespace Stratis.SmartContracts.Tools.Sct.Report
{
    /// <summary>
    /// Defines the structure of the data contained in a smart contract validation report.
    /// </summary>
    public class ValidationReportData
    {
        public string FileName { get; set; }

        public bool CompilationSuccess { get; set; }

        public List<CompilationError> CompilationErrors { get; set; }

        public byte[] CompilationBytes { get; set; }

        public bool FormatValid { get; set; }

        public List<ValidationError> FormatValidationErrors { get; set; }

        public bool DeterminismValid { get; set; }

        public List<ValidationResult> DeterminismValidationErrors { get; set; }
        
        public List<Warning> Warnings { get; set; }
    }

    public class CompilationError
    {
        public string Message { get; set; }
    }

    public class ValidationError
    {
        public string Message { get; set; }
    }

    public class Warning
    {
        public string Message { get; set; }
    }
}