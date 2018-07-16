using System.Collections.Generic;
using System.Linq;
using Stratis.SmartContracts.Tools.Sct.Report.Elements;

namespace Stratis.SmartContracts.Tools.Sct.Report.Sections
{
    /// <summary>
    /// Represents the section of a smart contract validation report
    /// that outputs determinism validation results.
    /// </summary>
    public class DeterminismSection : IReportSection
    {
        public IEnumerable<IReportElement> CreateSection(ValidationReportData data)
        {
            // If compilation failed we have nothing to validate
            if (!data.CompilationSuccess)
            {
                yield break;
            }

            yield return new ReportElement($"Determinism Validation Result");
            yield return new ReportElement($"Determinism Valid: {data.DeterminismValid}");

            yield return new NewLineElement();

            if (!data.DeterminismValid)
            {
                var grouped = data.DeterminismValidationErrors.GroupBy(x => x.SubjectName);
                foreach(var method in grouped)
                {
                    yield return new ReportElement($"{method.Key}:");

                    foreach(var error in method)
                    {
                        yield return new ReportElement($"   {error.Message}");
                    }

                    yield return new NewLineElement();
                }
            }
        }
    }
}