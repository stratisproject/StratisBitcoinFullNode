using System.Collections.Generic;
using Stratis.SmartContracts.Tools.Sct.Report.Elements;

namespace Stratis.SmartContracts.Tools.Sct.Report.Sections
{
    /// <summary>
    /// Represents the section of a smart contract validation report
    /// that outputs a footer.
    /// </summary>
    public class FooterSection : IReportSection
    {
        public IEnumerable<IReportElement> CreateSection(ValidationReportData data)
        {
            yield return new SpacerElement();
        }
    }
}