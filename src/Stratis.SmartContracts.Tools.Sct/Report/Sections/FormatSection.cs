using System.Collections.Generic;
using Stratis.SmartContracts.Tools.Sct.Report.Elements;

namespace Stratis.SmartContracts.Tools.Sct.Report.Sections
{
    /// <summary>
    /// Represents the section of a smart contract validation report
    /// that outputs format validation results.
    /// </summary>
    public class FormatSection : IReportSection
    {
        public IEnumerable<IReportElement> CreateSection(ValidationReportData data)
        {
            // If compilation failed we have nothing to validate
            if (!data.CompilationSuccess)
            {
                yield break;
            }

            yield return new ReportElement($"Format Validation Result");
            yield return new ReportElement($"Format Valid: {data.FormatValid}");

            foreach (var error in data.FormatValidationErrors)
            {
                yield return new ReportElement($"Error: {error.Message}");
            }

            yield return new NewLineElement();
        }
    }
}