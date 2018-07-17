using System.Collections.Generic;
using System.IO;
using Stratis.SmartContracts.Tools.Sct.Report.Elements;

namespace Stratis.SmartContracts.Tools.Sct.Report
{
    /// <summary>
    /// Renders report sections to a <see cref="T:System.IO.TextWriter" /> stream.
    /// Designed for use with <see cref="P:System.Console.Out" />
    /// </summary>
    public class StreamTextRenderer : BaseRenderer
    {
        private readonly TextWriter textWriter;

        public StreamTextRenderer(TextWriter textWriter)
        {
            this.textWriter = textWriter;
        }

        public override void Render(IEnumerable<IReportSection> sections, ValidationReportData data)
        {
            foreach (IReportSection section in sections)
            {
                IEnumerable<IReportElement> elements = section.CreateSection(data);

                foreach (dynamic element in elements)
                {
                    // Dynamically dispatch to a render method based on the element type
                    Render(element);
                }
            }
        }

        protected override void Render(ReportElement element)
        {
            this.textWriter.WriteLine(element.Text);
        }

        protected override void Render(SpacerElement element)
        {
            this.textWriter.WriteLine("======");
        }

        protected override void Render(HeaderElement element)
        {
            var headerElement = $"====== {element.Text} ======";

            this.textWriter.WriteLine(headerElement);
        }

        protected override void Render(NewLineElement element)
        {
            this.textWriter.WriteLine();
        }
    }
}
