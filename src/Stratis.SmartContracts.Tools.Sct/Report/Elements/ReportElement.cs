namespace Stratis.SmartContracts.Tools.Sct.Report.Elements
{
    /// <summary>
    /// Represents a report element containing text.
    /// </summary>
    public class ReportElement : IReportElement
    {
        public ReportElement(string text)
        {
            this.Text = text;
        }

        public string Text { get; set; }
    }
}