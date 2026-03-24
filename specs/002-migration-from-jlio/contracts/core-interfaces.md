# Contract: TLio.Core Public Interfaces

**Package**: `TLio.Core`
**Date**: 2026-03-24

These interfaces form the stable contract that all adapter, command, function, and client code depends on. Breaking changes to any interface require a major version bump.

---

## INodeAdapter\<TNode\>

**Namespace**: `TLio.Core.Contracts`

Format-specific node manipulation. Adapter assemblies implement this; commands and functions call through it.

```csharp
public interface INodeAdapter<TNode>
{
    // Type queries
    bool IsObject(TNode node);
    bool IsArray(TNode node);
    bool IsPrimitive(TNode node);
    bool IsNull(TNode node);

    // Object operations
    bool HasProperty(TNode node, string propertyName);
    TNode? GetProperty(TNode node, string propertyName);
    void SetProperty(TNode node, string propertyName, TNode value);
    void RemoveProperty(TNode node, string propertyName);
    IEnumerable<string> GetPropertyNames(TNode node);

    // Array operations
    void AppendToArray(TNode array, TNode value);
    void InsertIntoArray(TNode array, int index, TNode value);
    void RemoveFromArray(TNode array, int index);
    int GetArrayLength(TNode array);
    TNode GetArrayElement(TNode array, int index);
    IEnumerable<TNode> GetArrayElements(TNode array);

    // Node creation
    TNode CreateNull();
    TNode CreateObject();
    TNode CreateArray();
    TNode CreateString(string value);
    TNode CreateNumber(double value);
    TNode CreateBoolean(bool value);
    TNode CreateValue(object? value);

    // Value access
    object? GetValue(TNode node);
    T? GetValue<T>(TNode node);

    // Type coercion
    bool? TryGetBoolean(TNode node);
    double? TryGetDouble(TNode node);
    string? TryGetString(TNode node);

    // Cloning & replacement
    TNode DeepClone(TNode node);
    void Replace(TNode target, TNode replacement);
    bool RemoveFromParent(TNode node);

    // Deep merge
    void DeepMergeInto(TNode source, TNode target, ArrayMergeMode arrayMergeMode = ArrayMergeMode.Concat);

    // Parent access
    TNode? GetParentNode(TNode node);
    string? GetParentPropertyName(TNode node);

    // Equality
    bool DeepEquals(TNode a, TNode b);

    // Serialisation
    TNode Parse(string content);
    string Serialize(TNode node, bool pretty = false);
}

public enum ArrayMergeMode { Concat, Replace, MergeByKey }
```

---

## IItemsFetcher\<TNode\>

**Namespace**: `TLio.Core.Contracts`

Path-based node selection. Swap to change path language (JsonPath → XPath → custom).

```csharp
public interface IItemsFetcher<TNode>
{
    string RootPathIndicator { get; }
    string PathDelimiter { get; }
    string CurrentItemPathIndicator { get; }
    string ParentPathIndicator { get; }
    string ArrayCloseChar { get; }

    SelectedNodes<TNode> SelectNodes(string path, TNode data);
    TNode? SelectNode(string path, TNode data);
    string GetPath(TNode node);
    TNode? GetParent(TNode node, int levels = 1);
    string ResolveRelativePath(string relativePath, TNode currentNode, TNode dataContext);
    void EnsurePath(string path, TNode root, INodeAdapter<TNode> adapter);
    (string parentPath, string leafName) SplitParentAndLeaf(string path);
    string? ProcessIndirectPath(string path, TNode data);
    IEnumerable<string> GetIntellisense(string partialPath, TNode data);
}
```

---

## IExecutionContext\<TNode\>

**Namespace**: `TLio.Core.Contracts`

Runtime environment injected into every command and function. Single source of truth for adapter access.

```csharp
public interface IExecutionContext<TNode>
{
    IItemsFetcher<TNode> ItemsFetcher { get; set; }
    INodeAdapter<TNode> NodeAdapter { get; set; }
    IExecutionLogger Logger { get; set; }

    void LogWarning(string group, string message);
    void LogError(string group, string message);
    void LogInfo(string group, string message);
    LogEntries GetLogEntries();
}
```

---

## ICommand\<TNode\>

**Namespace**: `TLio.Core.Contracts`

Single script step following Find → Compute → Operate.

```csharp
public interface ICommand<TNode>
{
    string CommandName { get; }
    TLioExecutionResult<TNode> Execute(TNode data, IExecutionContext<TNode> context);
}
```

---

## IFunction\<TNode\>

**Namespace**: `TLio.Core.Contracts`

Value-producing expression callable from `=functionName(args)` in script values.

```csharp
public interface IFunction<TNode>
{
    string FunctionName { get; }
    FunctionResult<TNode> Execute(TNode currentNode, TNode dataContext,
                                   Arguments<TNode> arguments,
                                   IExecutionContext<TNode> context);
}
```

---

## IFunctionSupportedValue\<TNode\>

**Namespace**: `TLio.Core.Contracts`

A value argument that may be either a fixed literal or a dynamic function result.

```csharp
public interface IFunctionSupportedValue<TNode>
{
    FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context);
    string ToScript();
}
```
