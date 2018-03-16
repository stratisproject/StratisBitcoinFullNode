using System.Collections.Generic;
using Stratis.SmartContracts.Tools.Validation.Report.Elements;

namespace Stratis.SmartContracts.Tools.Validation.Report.Sections
{
    /// <summary>
    /// Represents the section of a smart contract validation report
    /// that outputs a header.
    /// </summary>
    public class HeaderSection : IReportSection
    {
        public IEnumerable<IReportElement> CreateSection(ValidationReportData data)
        {
            yield return new HeaderElement($"Smart Contract Validation results for file {data.FileName}");
        }
    }
}
