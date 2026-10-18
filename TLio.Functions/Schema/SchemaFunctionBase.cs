using System.Text.Json;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Functions.Schema;

/// <summary>
/// Shared argument handling for <c>orderBySchema</c>, <c>filterBySchema</c> and <c>skeleton</c>.
///
/// The schema argument is either a path to a schema node in the document
/// (<c>=orderBySchema($.schema)</c>) or JSON text (<c>=orderBySchema('{"properties":{…}}')</c>).
/// Reading it through the node adapter is what lets the schema sit in an XML or YAML document;
/// the text form is always JSON, whatever the document's format.
///
/// The functions hold no state between calls — a compiled script's functions are shared,
/// concurrently-executed singletons — so everything request-scoped lives in locals.
/// </summary>
public abstract class SchemaFunctionBase<TNode> : FunctionBase<TNode>
{
    protected bool TryReadSchema(
        IFunctionSupportedValue<TNode> arg,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context,
        out SchemaDocument<TNode> document)
    {
        document = null!;
        var adapter = context.NodeAdapter;

        var result = ResolveArg(arg, currentNode, dataContext, context);
        if (!result.Success || result.Data.Count != 1)
        {
            context.LogError(FunctionName, $"{FunctionName}: the schema argument did not resolve to exactly one node.");
            return false;
        }

        var schema = result.Data[0];
        if (!adapter.IsObject(schema) && adapter.TryGetString(schema) is { } text)
        {
            try
            {
                schema = SchemaDocument<TNode>.FromJsonText(text, adapter);
            }
            catch (JsonException ex)
            {
                context.LogError(FunctionName, $"{FunctionName}: the schema text is not valid JSON — {ex.Message}");
                return false;
            }
        }

        if (!adapter.IsObject(schema))
        {
            context.LogError(FunctionName, $"{FunctionName}: the schema argument is not a schema object.");
            return false;
        }

        document = new SchemaDocument<TNode>(schema, adapter, message => context.LogWarning(FunctionName, message));
        return true;
    }

    /// <summary>Resolve the data argument to the node to shape.</summary>
    protected bool TryReadData(
        IFunctionSupportedValue<TNode> arg,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context,
        out TNode data)
    {
        data = currentNode;

        var result = ResolveArg(arg, currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(FunctionName, $"{FunctionName}: the data argument path not found.");
            return false;
        }

        data = result.Data[0];
        return true;
    }
}
