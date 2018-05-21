using System.Collections.Generic;
using System.Linq;
using Stratis.SmartContracts.Tools.Sct.Report.Elements;

namespace Stratis.SmartContracts.Tools.Sct.Report.Sections
{
    /// <summary>
    /// Represents the section of a smart contract validation report
    /// that outputs warnings.
    /// </summary>
    public class WarningsSection : IReportSection
    {
        public IEnumerable<IReportElement> CreateSection(ValidationReportData data)
        {
            yield return new ReportElement($"Warnings");

            yield return new SpacerElement();

            yield return new NewLineElement();

            foreach (Warning warning in data.Warnings)
            {
                yield return new ReportElement($"   {warning.Message}");
            }

            yield return new NewLineElement();
        }
    }
}