using System.Collections.Generic;

namespace Stratis.SmartContracts.Tools.Validation.Report
{
    public interface IReportRenderer
    {
        void Render(IEnumerable<IReportSection> sections, ValidationReportData data);
    }
}