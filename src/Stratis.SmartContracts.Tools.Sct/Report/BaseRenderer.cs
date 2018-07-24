using System.Collections.Generic;
using Stratis.SmartContracts.Tools.Sct.Report.Elements;

namespace Stratis.SmartContracts.Tools.Sct.Report
{
    /// <summary>
    /// Defines the concrete element types that must be implemented 
    /// by a renderer.
    /// </summary>
    public abstract class BaseRenderer : IReportRenderer
    {
        public abstract void Render(IEnumerable<IReportSection> sections, ValidationReportData data);

        protected abstract void Render(ReportElement element);

        protected abstract void Render(SpacerElement element);

        protected abstract void Render(HeaderElement element);

        protected abstract void Render(NewLineElement element);
    }
}