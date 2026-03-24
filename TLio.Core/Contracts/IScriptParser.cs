using TLio.Core.Models;

namespace TLio.Core.Contracts;

/// <summary>
/// Parses a serialised script text into an executable TLioScript.
///
/// TLio scripts are always serialised as JSON (for backward compatibility with JLio)
/// regardless of the data format being processed. The parser is therefore separate
/// from the data-format adapters and lives in TLio.Client.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface IScriptParser<TNode>
{
    /// <summary>
    /// Parse a JSON-format script string into an executable script.
    /// The <paramref name="nodeConverter"/> is used to convert literal JSON values
    /// from the script into TNode instances for FixedValue arguments.
    /// </summary>
    TLioScript<TNode> Parse(string scriptJson, IScriptNodeConverter<TNode> nodeConverter);
}

/// <summary>
/// Converts a literal script value (expressed in JSON during script parsing) into
/// a TNode that can be written into the target data format.
///
/// This is the bridge between the always-JSON script format and the TNode data world.
/// For the Newtonsoft adapter this is essentially a no-op (JToken == JToken).
/// For System.Text.Json it converts JToken → JsonNode.
/// For XML it converts a JSON literal to an XElement.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface IScriptNodeConverter<TNode>
{
    /// <summary>
    /// Convert a raw JSON value (string, number, boolean, object, array, null)
    /// expressed as a plain object from the script parser into a TNode.
    /// </summary>
    TNode Convert(object? rawValue);

    /// <summary>
    /// Convert a JSON string literal from the script into a TNode string value.
    /// </summary>
    TNode ConvertString(string value);

    /// <summary>
    /// Convert a function-expression string (e.g. "=sum($.values)") into a
    /// TNode — used when function results need to be written as values.
    /// The adapter may need to process nested function calls recursively.
    /// </summary>
    TNode ConvertFunctionExpression(string expression, TNode currentNode, TNode dataContext, IExecutionContext<TNode> context);
}
