using System.Globalization;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;
using TLio.Mcp.Models;

namespace TLio.Mcp.Services;

public sealed class StructuralDiffService
{
    // ── Public entry points ─────────────────────────────────────────────────

    public List<ChangeItem> DiffJson(JToken input, JToken target)
    {
        var inputFlat = FlattenJson(input, "$");
        var targetFlat = FlattenJson(target, "$");
        return BuildChanges(inputFlat, targetFlat);
    }

    public List<ChangeItem> DiffXml(XElement input, XElement target)
    {
        var inputFlat = FlattenXml(input, "");
        var targetFlat = FlattenXml(target, "");
        return BuildChanges(inputFlat, targetFlat);
    }

    public List<ChangeItem> DiffYaml(YamlNode input, YamlNode target)
    {
        var inputFlat = FlattenYaml(input, "");
        var targetFlat = FlattenYaml(target, "");
        return BuildChanges(inputFlat, targetFlat);
    }

    public void AnnotateWithIntent(List<ChangeItem> changes, string intent)
    {
        var intentWords = intent.ToLowerInvariant()
            .Split([' ', ',', '.', ';', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        foreach (var change in changes)
        {
            var pathWords = ExtractPathSegments(change.SourcePath + " " + change.TargetPath);
            if (pathWords.Any(w => intentWords.Contains(w.ToLowerInvariant())))
                change.IntentAnnotation = $"Matches intent: '{intent}'.";
        }
    }

    public void ApplyRefinement(List<ChangeItem> changes, IReadOnlyList<CommandTraceRecord> priorTrace)
    {
        var successPaths = priorTrace
            .Where(t => t.Outcome.Equals("success", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var change in changes)
        {
            var matched = successPaths.Contains(change.SourcePath) ||
                          successPaths.Contains(change.TargetPath);
            change.Resolution = matched ? "resolved" : "unresolved";
        }
    }

    // ── Flatten ─────────────────────────────────────────────────────────────

    private static Dictionary<string, string?> FlattenJson(JToken node, string path)
    {
        var result = new Dictionary<string, string?>();
        FlattenJsonNode(node, path, result);
        return result;
    }

    private static void FlattenJsonNode(JToken node, string path, Dictionary<string, string?> result)
    {
        switch (node)
        {
            case JObject obj:
                if (!obj.Properties().Any())
                    result[path] = "{}";
                else
                    foreach (var prop in obj.Properties())
                        FlattenJsonNode(prop.Value, $"{path}.{prop.Name}", result);
                break;
            case JArray arr:
                if (arr.Count == 0)
                    result[path] = "[]";
                else
                    for (var i = 0; i < arr.Count; i++)
                        FlattenJsonNode(arr[i], $"{path}[{i}]", result);
                break;
            default:
                // Preserve type in the flattened value so bool true ≠ string "True" and
                // number 1 ≠ string "1". Use JSON canonical form: strings quoted, booleans
                // lowercase, numbers invariant-culture (dot decimal separator for valid json_value).
                result[path] = node.Type switch
                {
                    JTokenType.Null => null,
                    JTokenType.Boolean => node.Value<bool>() ? "true" : "false",
                    JTokenType.Integer => node.Value<long>().ToString(CultureInfo.InvariantCulture),
                    JTokenType.Float => node.Value<double>().ToString(CultureInfo.InvariantCulture),
                    JTokenType.String => $"\"{node}\"",
                    _ => node.ToString(Newtonsoft.Json.Formatting.None)
                };
                break;
        }
    }

    private static Dictionary<string, string?> FlattenXml(XElement element, string parentPath)
    {
        var result = new Dictionary<string, string?>();
        FlattenXmlElement(element, parentPath, result);
        return result;
    }

    private static void FlattenXmlElement(XElement el, string parentPath, Dictionary<string, string?> result)
    {
        var path = parentPath == "" ? $"/{el.Name.LocalName}" : $"{parentPath}/{el.Name.LocalName}";

        foreach (var attr in el.Attributes())
            result[$"{path}[@{attr.Name.LocalName}]"] = attr.Value;

        var children = el.Elements().ToList();
        if (children.Count == 0)
        {
            result[path] = el.Value;
            return;
        }

        // Group children by name to detect arrays
        var groups = children.GroupBy(c => c.Name.LocalName).ToList();
        foreach (var group in groups)
        {
            var items = group.ToList();
            if (items.Count > 1)
                for (var i = 0; i < items.Count; i++)
                    FlattenXmlElement(items[i], $"{path}/{group.Key}[{i}]", result);
            else
                FlattenXmlElement(items[0], path, result);
        }
    }

    private static Dictionary<string, string?> FlattenYaml(YamlNode node, string path)
    {
        var result = new Dictionary<string, string?>();
        FlattenYamlNode(node, path, result);
        return result;
    }

    private static void FlattenYamlNode(YamlNode node, string path, Dictionary<string, string?> result)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                if (!mapping.Children.Any())
                    result[path.Length > 0 ? path : "."] = "{}";
                else
                    foreach (var (key, value) in mapping.Children)
                    {
                        var keyStr = ((YamlScalarNode)key).Value ?? "";
                        var childPath = path.Length == 0 ? $".{keyStr}" : $"{path}.{keyStr}";
                        FlattenYamlNode(value, childPath, result);
                    }
                break;
            case YamlSequenceNode sequence:
                if (!sequence.Children.Any())
                    result[path] = "[]";
                else
                    for (var i = 0; i < sequence.Children.Count; i++)
                        FlattenYamlNode(sequence.Children[i], $"{path}[{i}]", result);
                break;
            case YamlScalarNode scalar:
                result[path.Length > 0 ? path : "."] = scalar.Value;
                break;
        }
    }

    // ── Diff ────────────────────────────────────────────────────────────────

    private static List<ChangeItem> BuildChanges(
        Dictionary<string, string?> input,
        Dictionary<string, string?> target)
    {
        var changes = new List<ChangeItem>();

        var removes = new List<(string Path, string? Value)>();
        var adds = new List<(string Path, string? Value)>();

        foreach (var (path, value) in input)
        {
            if (!target.TryGetValue(path, out var targetValue))
                removes.Add((path, value));
            else if (targetValue != value)
                changes.Add(new ChangeItem
                {
                    SourcePath = path,
                    TargetPath = path,
                    ChangeType = "Mutate",
                    Description = $"MUTATE '{path}': current value is {value}, must become {targetValue}. Command: set, path: \"{path}\". json_value: {targetValue}."
                });
        }

        foreach (var (path, value) in target)
        {
            if (!input.ContainsKey(path))
                adds.Add((path, value));
        }

        // Rename heuristic: Remove + Add with identical value → Rename
        var usedRemoves = new HashSet<string>();
        var usedAdds = new HashSet<string>();

        foreach (var remove in removes)
        {
            foreach (var add in adds)
            {
                if (usedRemoves.Contains(remove.Path) || usedAdds.Contains(add.Path)) continue;
                if (remove.Value == add.Value)
                {
                    changes.Add(new ChangeItem
                    {
                        SourcePath = remove.Path,
                        TargetPath = add.Path,
                        ChangeType = "Rename",
                        Description = $"RENAME '{remove.Path}' → '{add.Path}': field is moving (value unchanged: {remove.Value}). Commands: copy fromPath=\"{remove.Path}\" toPath=\"{add.Path}\", then remove path=\"{remove.Path}\"."
                    });
                    usedRemoves.Add(remove.Path);
                    usedAdds.Add(add.Path);
                    break;
                }
            }
        }

        // Remaining Removes and Adds after rename detection
        var remainingRemoves = removes.Where(r => !usedRemoves.Contains(r.Path)).ToList();
        var remainingAdds = adds.Where(a => !usedAdds.Contains(a.Path)).ToList();

        // Reorder heuristic: same values at different array-index paths
        var reorderRemoves = new HashSet<string>();
        var reorderAdds = new HashSet<string>();
        foreach (var remove in remainingRemoves)
        {
            foreach (var add in remainingAdds)
            {
                if (reorderRemoves.Contains(remove.Path) || reorderAdds.Contains(add.Path)) continue;
                if (remove.Value == add.Value &&
                    PathDiffersOnlyInIndex(remove.Path, add.Path))
                {
                    changes.Add(new ChangeItem
                    {
                        SourcePath = remove.Path,
                        TargetPath = add.Path,
                        ChangeType = "Reorder",
                        Description = $"REORDER: value {remove.Value} moved from '{remove.Path}' to '{add.Path}'. Array element reordering — consider rebuilding the array with values in the correct order."
                    });
                    reorderRemoves.Add(remove.Path);
                    reorderAdds.Add(add.Path);
                    break;
                }
            }
        }

        foreach (var remove in remainingRemoves.Where(r => !reorderRemoves.Contains(r.Path)))
            changes.Add(new ChangeItem
            {
                SourcePath = remove.Path,
                TargetPath = "",
                ChangeType = "Remove",
                Description = $"REMOVE '{remove.Path}': field exists in source (value: {remove.Value}) but is absent from target. Command: remove, path: \"{remove.Path}\"."
            });

        foreach (var add in remainingAdds.Where(a => !reorderAdds.Contains(a.Path)))
            changes.Add(new ChangeItem
            {
                SourcePath = "",
                TargetPath = add.Path,
                ChangeType = "Add",
                Description = $"ADD '{add.Path}': field is missing from source, must be created. Command: add, path: \"{add.Path}\". json_value: {add.Value}."
            });

        return changes;
    }

    private static bool PathDiffersOnlyInIndex(string a, string b)
    {
        // Replace all [N] with [0] and compare
        var normalized = Regex.Replace(a, @"\[\d+\]", "[0]");
        var normalizedB = Regex.Replace(b, @"\[\d+\]", "[0]");
        return normalized == normalizedB && a != b;
    }

    private static IEnumerable<string> ExtractPathSegments(string path)
    {
        return Regex.Split(path, @"[\.\[\]/\$]")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && !int.TryParse(s, out _));
    }
}
