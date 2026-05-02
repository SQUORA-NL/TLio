using System.Globalization;
using System.Text;

namespace TLio.Extensions.ETL.Commands;

/// <summary>
/// Converts a node (object or array of objects) to a CSV string and replaces the
/// node with that string in the document.
///
/// Columns are sorted alphabetically. Metadata (_-prefixed) and type (_type)
/// columns are excluded by default.
///
/// Ported from JLio.Extensions.ETL.Commands.ToCsv. All node inspection uses
/// INodeAdapter — no format-specific code.
/// </summary>
public class ToCsv<TNode> : CommandBase<TNode>
{
    public override string CommandName => "tocsv";

    public string Path { get; set; } = "$";
    public CsvSettings CsvSettings { get; set; } = new();

    private readonly StringBuilder _csv = new(8192);
    private readonly StringBuilder _escape = new(512);

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path, TraceOutcome.Failure, 0,
                $"tocsv: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        var csvCount = 0;
        try
        {
            var targets = context.ItemsFetcher.SelectNodes(Path, dataContext).ToList();
            csvCount = targets.Count;
            foreach (var target in targets)
            {
                var csv = BuildCsv(target, context.NodeAdapter, context);
                context.NodeAdapter.Replace(target, context.NodeAdapter.CreateString(csv));
            }
        }
        catch (Exception ex)
        {
            context.LogError(CoreConstants.CommandExecution, $"tocsv: {ex.Message}");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path, TraceOutcome.Failure, 0,
                $"tocsv: error — {ex.Message}"));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path,
            csvCount == 0 ? TraceOutcome.NoOp : TraceOutcome.Success,
            csvCount,
            csvCount == 0
                ? $"tocsv: path '{Path}' matched 0 nodes; nothing converted."
                : $"tocsv: converted {csvCount} node(s) at '{Path}' to CSV."));
        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    private string BuildCsv(TNode node, INodeAdapter<TNode> adapter, IExecutionContext<TNode> context)
    {
        List<Dictionary<string, TNode>> rows;

        if (adapter.IsObject(node))
        {
            rows = new List<Dictionary<string, TNode>> { ExtractRow(node, adapter) };
        }
        else if (adapter.IsArray(node))
        {
            rows = new List<Dictionary<string, TNode>>();
            foreach (var item in adapter.GetArrayElements(node))
                if (adapter.IsObject(item))
                    rows.Add(ExtractRow(item, adapter));
        }
        else
        {
            context.LogWarning(CoreConstants.CommandExecution,
                "tocsv: can only convert objects or arrays of objects");
            return "";
        }

        if (rows.Count == 0) return "";

        // Collect and sort all column names
        var colSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
            foreach (var k in row.Keys) colSet.Add(k);
        var columns = colSet.ToArray();
        Array.Sort(columns, StringComparer.Ordinal);

        _csv.Clear();

        if (CsvSettings.IncludeHeaders)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                if (i > 0) _csv.Append(CsvSettings.Delimiter);
                _csv.Append(EscapeField(columns[i]));
            }
            _csv.AppendLine();
        }

        foreach (var row in rows)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                if (i > 0) _csv.Append(CsvSettings.Delimiter);
                var val = row.TryGetValue(columns[i], out var v)
                    ? FormatValue(v, adapter)
                    : CsvSettings.NullValueRepresentation;
                _csv.Append(EscapeField(val));
            }
            _csv.AppendLine();
        }

        return _csv.ToString().TrimEnd('\r', '\n');
    }

    private Dictionary<string, TNode> ExtractRow(TNode obj, INodeAdapter<TNode> adapter)
    {
        var row = new Dictionary<string, TNode>(StringComparer.Ordinal);
        foreach (var name in adapter.GetPropertyNames(obj))
        {
            if (!ShouldIncludeColumn(name)) continue;
            row[name] = adapter.GetProperty(obj, name)!;
        }
        return row;
    }

    private bool ShouldIncludeColumn(string name)
    {
        if (!CsvSettings.IncludeMetadata &&
            (name.Length > 0 && name[0] == '_' || name.Contains("Metadata")))
            return false;
        if (!CsvSettings.IncludeTypeColumns && name.EndsWith(CsvSettings.TypeColumnSuffix))
            return false;
        return true;
    }

    private string FormatValue(TNode node, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsNull(node)) return CsvSettings.NullValueRepresentation;

        var boolVal = adapter.TryGetBoolean(node);
        if (boolVal != null)
        {
            var parts = CsvSettings.BooleanFormat.Split(',');
            return boolVal.Value ? parts[0] : (parts.Length > 1 ? parts[1] : parts[0]);
        }

        var dblVal = adapter.TryGetDouble(node);
        if (dblVal != null) return dblVal.Value.ToString(CultureInfo.InvariantCulture);

        if (adapter.IsObject(node) || adapter.IsArray(node))
            return adapter.Serialize(node, false);

        return adapter.TryGetString(node) ?? CsvSettings.NullValueRepresentation;
    }

    private string EscapeField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return CsvSettings.QuoteAllFields ? $"\"{field}\"" : field;

        bool needsQuoting = CsvSettings.QuoteAllFields
            || field.Contains(CsvSettings.Delimiter)
            || field.Contains(CsvSettings.EscapeQuoteChar)
            || field.IndexOfAny(new[] { '\r', '\n' }) >= 0
            || field[0] == ' '
            || field[field.Length - 1] == ' ';

        if (!needsQuoting) return field;

        _escape.Clear();
        _escape.Append(CsvSettings.EscapeQuoteChar);
        foreach (var c in field)
        {
            _escape.Append(c);
            if (c.ToString() == CsvSettings.EscapeQuoteChar) _escape.Append(CsvSettings.EscapeQuoteChar);
        }
        _escape.Append(CsvSettings.EscapeQuoteChar);
        return _escape.ToString();
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.ValidationMessages.Add("Path property is required for toCsv command");
        if (CsvSettings == null)
            result.ValidationMessages.Add("CsvSettings property is required for toCsv command");
        else
        {
            if (string.IsNullOrEmpty(CsvSettings.Delimiter))
                result.ValidationMessages.Add("Delimiter cannot be empty in CsvSettings");
            if (string.IsNullOrEmpty(CsvSettings.EscapeQuoteChar))
                result.ValidationMessages.Add("EscapeQuoteChar cannot be empty in CsvSettings");
            if (string.IsNullOrEmpty(CsvSettings.BooleanFormat) || !CsvSettings.BooleanFormat.Contains(','))
                result.ValidationMessages.Add("BooleanFormat must contain comma-separated true,false values");
        }
        return result;
    }
}
