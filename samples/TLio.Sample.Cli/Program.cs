using TLio.Client;
using TLio.Core.Models;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;

// ── Argument parsing ─────────────────────────────────────────────────────────

string? inputPath  = null;
string? scriptPath = null;
string? outputPath = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--input"  when i + 1 < args.Length: inputPath  = args[++i]; break;
        case "--script" when i + 1 < args.Length: scriptPath = args[++i]; break;
        case "--output" when i + 1 < args.Length: outputPath = args[++i]; break;
        case "--help":
            Console.WriteLine("""
                TLio.Sample.Cli — transform a data file using a TLio script

                Usage:
                  TLio.Sample.Cli --input <path> --script <path> [--output <path>]

                Arguments:
                  --input  <path>   Path to the input data file (.json, .xml, .yaml, .yml)
                  --script <path>   Path to the TLio script file (.json)
                  --output <path>   (optional) Write transformed output to this file instead of stdout
                  --help            Show this help and exit

                Exit codes:
                  0  Transformation succeeded
                  1  Input file not found
                  2  Script file not found
                  3  Input file could not be parsed
                  4  Script file could not be parsed
                  5  Transformation execution failed
                 10  Unexpected error
                """);
            return 0;
    }
}

// ── Validation ───────────────────────────────────────────────────────────────

if (string.IsNullOrEmpty(inputPath) || string.IsNullOrEmpty(scriptPath))
{
    Console.Error.WriteLine("Error: --input and --script are required. Use --help for usage.");
    return 1;
}

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
    return 1;
}

if (!File.Exists(scriptPath))
{
    Console.Error.WriteLine($"Error: Script file not found: {scriptPath}");
    return 2;
}

// ── Format detection ─────────────────────────────────────────────────────────

var ext = Path.GetExtension(inputPath).ToLowerInvariant();
var format = ext switch
{
    ".json"          => "json",
    ".xml"           => "xml",
    ".yaml" or ".yml"=> "yaml",
    _                => null
};

if (format is null)
{
    Console.Error.WriteLine($"Error: Unknown file extension '{ext}'. Supported: .json, .xml, .yaml, .yml");
    return 3;
}

// ── Execute ───────────────────────────────────────────────────────────────────

try
{
    var inputText  = File.ReadAllText(inputPath);
    var scriptText = File.ReadAllText(scriptPath);

    string output;

    try
    {
        output = RunTransform(format, inputText, scriptText);
    }
    catch (FormatException ex)
    {
        Console.Error.WriteLine($"Error: Could not parse input: {ex.Message}");
        return 3;
    }
    catch (System.Text.Json.JsonException ex)
    {
        Console.Error.WriteLine($"Error: Could not parse script: {ex.Message}");
        return 4;
    }
    catch (TransformFailedException ex)
    {
        Console.Error.WriteLine($"Error: Transformation failed.");
        foreach (var entry in ex.LogEntries)
            Console.Error.WriteLine($"  [{entry.Level}] {entry.Message}");
        return 5;
    }

    if (outputPath is not null)
        File.WriteAllText(outputPath, output);
    else
        Console.Write(output);

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    return 10;
}

// ── Transform dispatch ───────────────────────────────────────────────────────

static string RunTransform(string format, string inputText, string scriptText)
{
    return format switch
    {
        "json" => Execute(JsonExecutionContext.CreateDefault(),        inputText, scriptText),
        "xml"  => Execute(XmlExecutionContext.CreateWithNativeXPath(), inputText, scriptText),
        "yaml" => Execute(YamlExecutionContext.CreateDefault(),        inputText, scriptText),
        _      => throw new ArgumentException($"Unknown format: {format}")
    };
}

static string Execute<TNode>(ExecutionContext<TNode> context, string inputText, string scriptText)
{
    var adapter = context.NodeAdapter;

    TNode input;
    try { input = adapter.Parse(inputText); }
    catch (Exception ex) { throw new FormatException(ex.Message, ex); }

    var options = ParseOptions<TNode>.CreateDefault();
    var engine  = new ScriptEngine<TNode>(options.CommandsProvider, options.FunctionsProvider);
    var result  = engine.Execute(scriptText, input, context);

    if (!result.Success)
        throw new TransformFailedException(context.GetLogEntries());

    return adapter.Serialize(result.Data);
}

// ── Helpers ──────────────────────────────────────────────────────────────────

internal sealed class TransformFailedException(
    IReadOnlyList<TLio.Core.Models.Logging.LogEntry> logEntries) : Exception
{
    public IReadOnlyList<TLio.Core.Models.Logging.LogEntry> LogEntries { get; } = logEntries;
}
