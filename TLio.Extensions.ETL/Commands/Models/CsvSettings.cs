namespace TLio.Extensions.ETL.Commands.Models;

/// <summary>Controls CSV output formatting for the ToCsv command.</summary>
public class CsvSettings
{
    public string Delimiter { get; set; } = ",";
    public bool IncludeHeaders { get; set; } = true;
    public bool IncludeTypeColumns { get; set; } = false;
    public string TypeColumnSuffix { get; set; } = "_type";
    public bool QuoteAllFields { get; set; } = false;
    public string EscapeQuoteChar { get; set; } = "\"";
    public string LineEnding { get; set; } = "\r\n";
    public string Encoding { get; set; } = "UTF-8";
    public bool IncludeMetadata { get; set; } = false;
    public string NullValueRepresentation { get; set; } = "";
    public string BooleanFormat { get; set; } = "true,false";
}
