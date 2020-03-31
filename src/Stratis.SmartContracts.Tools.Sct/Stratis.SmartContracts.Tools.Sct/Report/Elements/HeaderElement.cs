namespace Stratis.SmartContracts.Tools.Sct.Report.Elements
{
    /// <summary>
    /// Represents a report header element that contains text.
    /// </summary>
    public class HeaderElement : IReportElement
    {
        public HeaderElement(string text)
        {
            this.Text = text;
        }

        public string Text { get; set; }
    }
}