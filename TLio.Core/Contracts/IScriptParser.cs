using TLio.Core.Models;

namespace TLio.Core.Contracts;

/// <summary>
/// Turns script text into an executable <see cref="TLioScript{TNode}"/>.
///
/// A script can be written in any of three notations — JSON, XML or YAML — and they describe
/// the same command set with the same property names, so one implementation exists per
/// notation and they are interchangeable. Which notation a script is written in is independent
/// of the data format it transforms; only the paths inside the script have to speak the target
/// format's path language.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface IScriptParser<TNode>
{
    /// <summary>The notation this parser reads.</summary>
    ScriptFormat Format { get; }

    /// <summary>
    /// Parse script text into an executable script. Text that does not parse yields an empty
    /// script rather than an exception, and an unrecognised command name becomes a command
    /// that reports itself as not found when the script runs.
    /// </summary>
    TLioScript<TNode> ParseScript(string scriptText);
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
