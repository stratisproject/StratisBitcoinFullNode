using System.Collections.Generic;

namespace Stratis.SmartContracts.Tools.Sct.Report
{
    /// <summary>
    /// A grouping of related elements in a report.
    /// </summary>
    public interface IReportSection
    {
        // Populate the section with the given data
        IEnumerable<IReportElement> CreateSection(ValidationReportData data);
    }
}