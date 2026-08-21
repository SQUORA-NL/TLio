using NUnit.Framework;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace FormatConverter.Tests;

/// <summary>
/// Structural comparison of two YAML documents.
/// </summary>
/// <remarks>
/// Assertions that scraped key names out of the emitted text used to pass on YAML that YAML
/// itself could not read — the emitter indented the second key of an array item one level too
/// deep and nothing noticed. Everything here re-parses first, so invalid output fails loudly,
/// and compares the node trees, so indentation and quoting style stay free to differ while
/// structure and values do not.
/// </remarks>
public static class YamlAssert
{
    /// <summary>Assert that two YAML documents describe the same tree.</summary>
    public static void Equivalent(string actual, string expected, string? because = null)
    {
        var actualRoot = ParseOrFail(actual, "actual");
        var expectedRoot = ParseOrFail(expected, "expected");

        var difference = Compare(actualRoot, expectedRoot, "$");
        if (difference is not null)
            Assert.Fail($"{because ?? "YAML documents differ"}: {difference}\n\n--- actual ---\n{actual}\n--- expected ---\n{expected}");
    }

    /// <summary>Parse YAML, failing the test with the parser's own message if it will not read.</summary>
    public static YamlNode ParseOrFail(string yaml, string label)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count == 0)
                return new YamlMappingNode();
            return stream.Documents[0].RootNode;
        }
        catch (YamlException ex)
        {
            Assert.Fail($"The {label} YAML does not parse: {ex.Message}\n\n{yaml}");
            throw; // unreachable — Assert.Fail throws
        }
    }

    private static string? Compare(YamlNode actual, YamlNode expected, string path)
    {
        switch (expected)
        {
            case YamlMappingNode expectedMap:
                if (actual is not YamlMappingNode actualMap)
                    return $"at {path}: expected a mapping, found {Describe(actual)}";

                foreach (var entry in expectedMap.Children)
                {
                    var key = KeyOf(entry.Key);
                    if (!actualMap.Children.TryGetValue(entry.Key, out var actualValue))
                        return $"at {path}: missing key '{key}' (present: {string.Join(", ", actualMap.Children.Keys.Select(KeyOf))})";

                    var nested = Compare(actualValue, entry.Value, $"{path}.{key}");
                    if (nested is not null) return nested;
                }

                if (actualMap.Children.Count != expectedMap.Children.Count)
                {
                    var extra = actualMap.Children.Keys.Select(KeyOf)
                        .Except(expectedMap.Children.Keys.Select(KeyOf));
                    return $"at {path}: unexpected keys {string.Join(", ", extra)}";
                }
                return null;

            case YamlSequenceNode expectedSeq:
                if (actual is not YamlSequenceNode actualSeq)
                    return $"at {path}: expected a sequence, found {Describe(actual)}";
                if (actualSeq.Children.Count != expectedSeq.Children.Count)
                    return $"at {path}: expected {expectedSeq.Children.Count} items, found {actualSeq.Children.Count}";

                for (var i = 0; i < expectedSeq.Children.Count; i++)
                {
                    var nested = Compare(actualSeq.Children[i], expectedSeq.Children[i], $"{path}[{i}]");
                    if (nested is not null) return nested;
                }
                return null;

            case YamlScalarNode expectedScalar:
                if (actual is not YamlScalarNode actualScalar)
                    return $"at {path}: expected the scalar '{expectedScalar.Value}', found {Describe(actual)}";
                return actualScalar.Value == expectedScalar.Value
                    ? null
                    : $"at {path}: expected '{expectedScalar.Value}', found '{actualScalar.Value}'";

            default:
                return null;
        }
    }

    private static string KeyOf(YamlNode key) => key is YamlScalarNode s ? s.Value ?? "" : key.ToString();

    private static string Describe(YamlNode node) => node switch
    {
        YamlMappingNode m => $"a mapping of {m.Children.Count}",
        YamlSequenceNode s => $"a sequence of {s.Children.Count}",
        YamlScalarNode s => $"the scalar '{s.Value}'",
        _ => node.GetType().Name,
    };
}
