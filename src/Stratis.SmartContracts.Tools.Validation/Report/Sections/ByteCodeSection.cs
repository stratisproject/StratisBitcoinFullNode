using System.Collections.Generic;
using Stratis.SmartContracts.Tools.Validation.Report.Elements;

namespace Stratis.SmartContracts.Tools.Validation.Report.Sections
{
    /// <summary>
    /// Represents the section of a smart contract validation report
    /// that outputs the bytecode of a compiled contract.
    /// </summary>
    public class ByteCodeSection : IReportSection
    {
        public IEnumerable<IReportElement> CreateSection(ValidationReportData data)
        {
            if (data.CompilationSuccess)
            {
                yield return new ReportElement("ByteCode");
                yield return new ReportElement(data.CompilationBytes.ToHexString());
            }
        }
    }
}