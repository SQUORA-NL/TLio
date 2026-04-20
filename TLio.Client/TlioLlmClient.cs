using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TLio.Client;

/// <summary>
/// HTTP client for the locally-running tlio-llm Ollama model.
/// Default endpoint: http://localhost:11434
///
/// Usage:
///   var llm = new TlioLlmClient();
///   string script = await llm.WriteScriptAsync("rename field 'name' to 'fullName'");
///   string explain = await llm.ExplainScriptAsync(scriptJson);
/// </summary>
public sealed class TlioLlmClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <param name="baseUrl">Ollama base URL, default http://localhost:11434</param>
    /// <param name="modelName">Ollama model name, default tlio-llm</param>
    /// <param name="timeoutSeconds">HTTP timeout in seconds, default 120</param>
    public TlioLlmClient(
        string baseUrl = "http://localhost:11434",
        string modelName = "tlio-llm",
        int timeoutSeconds = 120)
    {
        _model = modelName;
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    // ────────────────────────────────────────────────────────────────
    // High-level helpers
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ask the model to write a TLio script from a natural-language description.
    /// Optionally include example input JSON to give the model more context.
    /// </summary>
    public Task<string> WriteScriptAsync(
        string instruction,
        string? inputJson = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = inputJson is { Length: > 0 }
            ? $"Transform this JSON with TLio:\n```json\n{inputJson}\n```\n{instruction}"
            : instruction;

        return GenerateAsync(prompt, cancellationToken);
    }

    /// <summary>
    /// Ask the model to explain what a TLio script does, step by step.
    /// </summary>
    public Task<string> ExplainScriptAsync(
        string scriptJson,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Explain what this TLio transformation script does:\n```json\n{scriptJson}\n```";
        return GenerateAsync(prompt, cancellationToken);
    }

    /// <summary>
    /// Ask the model to debug / fix a broken TLio script.
    /// </summary>
    public Task<string> DebugScriptAsync(
        string brokenScriptJson,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder("There's a bug in this TLio transformation. What's wrong and how do I fix it?\n");
        sb.Append($"```json\n{brokenScriptJson}\n```");
        if (errorMessage is { Length: > 0 })
            sb.Append($"\n\nError: {errorMessage}");
        return GenerateAsync(sb.ToString(), cancellationToken);
    }

    /// <summary>
    /// Ask any free-form question about TLio.
    /// </summary>
    public Task<string> AskAsync(
        string question,
        CancellationToken cancellationToken = default)
        => GenerateAsync(question, cancellationToken);

    // ────────────────────────────────────────────────────────────────
    // Core: single generate call (non-streaming)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Send a raw prompt and return the complete response text.
    /// </summary>
    public async Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaRequest(_model, prompt, Stream: false);
        using var response = await _http
            .PostAsJsonAsync("/api/generate", request, _json, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<OllamaResponse>(_json, cancellationToken)
            .ConfigureAwait(false);

        return result?.Response?.Trim() ?? string.Empty;
    }

    // ────────────────────────────────────────────────────────────────
    // Core: streaming generate (yields tokens as they arrive)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stream response tokens as they arrive from the model.
    /// Useful for showing live output in a UI or terminal.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new OllamaRequest(_model, prompt, Stream: true);
        var content = JsonContent.Create(request, options: _json);

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/generate") { Content = content };
        using var response = await _http
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        // StreamReader takes ownership — it will dispose the stream
        using var reader = new System.IO.StreamReader(
            await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false));
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaResponse>(line, _json);
            if (chunk?.Response is { Length: > 0 } token)
                yield return token;

            if (chunk?.Done == true) break;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // Health check
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the Ollama server is reachable and the model is listed.
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http
                .GetFromJsonAsync<OllamaTagsResponse>("/api/tags", _json, cancellationToken)
                .ConfigureAwait(false);

            return response?.Models?.Any(m =>
                m.Name.StartsWith(_model, StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();

    // ────────────────────────────────────────────────────────────────
    // Internal DTO types
    // ────────────────────────────────────────────────────────────────

    private record OllamaRequest(
        [property: JsonPropertyName("model")]  string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed class OllamaResponse
    {
        [JsonPropertyName("response")] public string? Response { get; init; }
        [JsonPropertyName("done")]     public bool Done        { get; init; }
    }

    private sealed class OllamaTagsResponse
    {
        [JsonPropertyName("models")] public List<OllamaModel>? Models { get; init; }
    }

    private sealed class OllamaModel
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    }
}
