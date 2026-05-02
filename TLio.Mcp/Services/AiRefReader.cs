using Microsoft.Extensions.Options;
using TLio.Mcp.Configuration;

namespace TLio.Mcp.Services;

public sealed class AiRefReader
{
    private readonly string _aiRefRoot;

    public AiRefReader(IOptions<McpConfiguration> config)
    {
        _aiRefRoot = ResolveRoot(config.Value.AiRefRoot);
    }

    public IReadOnlyList<(string Name, string Intent)> ListCommands()
        => ListItems(Path.Combine(_aiRefRoot, "commands"));

    public IReadOnlyList<(string Name, string Intent)> ListFunctions()
        => ListItems(Path.Combine(_aiRefRoot, "functions"));

    public string GetGuide()
    {
        var path = Path.Combine(_aiRefRoot, "guide.md");
        return File.Exists(path) ? File.ReadAllText(path) : "Guide not found. Call tlio_describe for individual command or function documentation.";
    }

    public string? GetContent(string name, string? type)
    {
        var file = FindFile(name, type);
        return file is null ? null : File.ReadAllText(file);
    }

    public string[] GetSuggestions(string name, string? type)
    {
        var dirs = GetSearchDirs(type);
        return dirs
            .SelectMany(d => Directory.Exists(d) ? Directory.EnumerateFiles(d, "*.md") : [])
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .Where(n => Levenshtein(n.ToLowerInvariant(), name.ToLowerInvariant()) <= 2)
            .ToArray();
    }

    private static IReadOnlyList<(string Name, string Intent)> ListItems(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "*.md")
            .OrderBy(f => f)
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var intent = ExtractIntent(f);
                return (name, intent);
            })
            .ToList();
    }

    private static string ExtractIntent(string filePath)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith('#')) continue;
            if (trimmed.StartsWith('>'))
                return trimmed.TrimStart('>', ' ');
            return trimmed;
        }
        return "";
    }

    private string? FindFile(string name, string? type)
    {
        foreach (var dir in GetSearchDirs(type))
        {
            var candidate = Path.Combine(dir, $"{name}.md");
            if (File.Exists(candidate)) return candidate;
            // case-insensitive fallback on case-sensitive file systems
            if (Directory.Exists(dir))
            {
                var found = Directory.EnumerateFiles(dir, "*.md")
                    .FirstOrDefault(f => string.Equals(
                        Path.GetFileNameWithoutExtension(f), name,
                        StringComparison.OrdinalIgnoreCase));
                if (found is not null) return found;
            }
        }
        return null;
    }

    private IEnumerable<string> GetSearchDirs(string? type)
    {
        if (type?.ToLowerInvariant() == "command")
            return [Path.Combine(_aiRefRoot, "commands")];
        if (type?.ToLowerInvariant() == "function")
            return [Path.Combine(_aiRefRoot, "functions")];
        if (type?.ToLowerInvariant() == "adapter")
            return [Path.Combine(_aiRefRoot, "adapters")];
        return [
            Path.Combine(_aiRefRoot, "commands"),
            Path.Combine(_aiRefRoot, "functions"),
            Path.Combine(_aiRefRoot, "adapters")
        ];
    }

    private static string ResolveRoot(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "ai-ref");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "docs", "ai-ref");
    }

    private static int Levenshtein(string a, string b)
    {
        var m = a.Length; var n = b.Length;
        var d = new int[m + 1, n + 1];
        for (var i = 0; i <= m; i++) d[i, 0] = i;
        for (var j = 0; j <= n; j++) d[0, j] = j;
        for (var i = 1; i <= m; i++)
            for (var j = 1; j <= n; j++)
                d[i, j] = a[i - 1] == b[j - 1] ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j - 1], Math.Min(d[i - 1, j], d[i, j - 1]));
        return d[m, n];
    }
}
