using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions;

/// <summary>
/// =datetime(format?) — returns the current UTC date/time as a string.
///
/// When no format argument is provided, returns ISO 8601 (yyyy-MM-ddTHH:mm:ss.fffZ).
/// Format tokens mirror JLio's datetime function:
///   yyyy, MM, dd, HH, mm, ss — standard .NET date/time format components.
///   Any other string is passed directly to DateTime.UtcNow.ToString(format).
///
/// Ported from JLio's Datetime function.
/// </summary>
public class Datetime<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => "datetime";

    private const string DefaultFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public override FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context)
    {
        var format = DefaultFormat;

        if (Arguments.Count > 0)
        {
            var fmtResult = Arguments[0].GetValue(currentNode, dataContext, context);
            if (fmtResult.Success && fmtResult.Data.First != null)
            {
                var str = context.NodeAdapter.TryGetString(fmtResult.Data.First);
                if (!string.IsNullOrEmpty(str)) format = str;
            }
        }

        var now = DateTime.UtcNow;
        string formatted;
        try
        {
            formatted = now.ToString(format);
        }
        catch
        {
            formatted = now.ToString(DefaultFormat);
        }

        return FunctionResult<TNode>.Successful(context.NodeAdapter.CreateString(formatted));
    }
}
