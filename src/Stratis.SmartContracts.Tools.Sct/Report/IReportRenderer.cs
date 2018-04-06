using System.Collections.Generic;

namespace Stratis.SmartContracts.Tools.Sct.Report
{
    public interface IReportRenderer
    {
        void Render(IEnumerable<IReportSection> sections, ValidationReportData data);
    }
}