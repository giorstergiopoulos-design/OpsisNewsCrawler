using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

public sealed record AiMessage(string Role, string Text);

public sealed class AiException : Exception
{
    public AiException(string message) : base(message) { }
}

/// <summary>
/// A small question-and-answer helper for the markets window, using the Anthropic Messages API with the user's OWN key.
/// It is off until the user has pasted a key; nothing is sent unless the user presses Send, and the user's prices and portfolio are
/// included only when "share my data" is ticked. The key stays on this PC (encrypted for the Windows account). It is not investment advice.
/// </summary>
public static class AiAssistant
{
    public const string DefaultModel = "claude-sonnet-5-5";
    public static readonly string[] Models = { "claude-sonnet-5-5", "claude-opus-5-5", "claude-haiku-5-5" };

    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler");
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        return client;
    }

    /// <summary>Sends the conversation and returns the answer text. Throws <see cref="AiException"/> with a message that can be shown to the user.</summary>
    public static async Task<string> AskAsync(string apiKey, string model, string system, IReadOnlyList<AiMessage> conversation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new AiException("no key");
        var body = JsonSerializer.Serialize(new
        {
            model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
            max_tokens = 1500,
            system,
            messages = conversation.Select(m => new { role = m.Role, content = m.Text }).ToArray()
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue("application/json")) };
        request.Headers.Add("x-api-key", apiKey.Trim());
        try
        {
            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new AiException(ErrorText((int)response.StatusCode, text));
            return ParseAnswer(text);
        }
        catch (HttpRequestException ex) { throw new AiException(ex.Message); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new AiException("timeout"); }
    }

    /// <summary>The text of an answer JSON (the text blocks joined); an <see cref="AiException"/> when there is none.</summary>
    public static string ParseAnswer(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var parts = doc.RootElement.GetProperty("content").EnumerateArray()
                .Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(b => b.GetProperty("text").GetString() ?? "");
            var answer = string.Join("\n", parts).Trim();
            if (answer.Length == 0) throw new AiException("empty answer");
            return answer;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiException("unexpected answer");
        }
    }

    private static string ErrorText(int status, string json)
    {
        var detail = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m)) detail = m.GetString() ?? "";
        }
        catch (JsonException) { }
        return detail.Length > 0 ? $"{status}: {detail}" : status.ToString();
    }

    /// <summary>The standing instruction of the assistant.</summary>
    public static string SystemPrompt(bool greek, string? dataBlock)
    {
        var sb = new StringBuilder();
        sb.Append(greek
            ? "Είσαι βοηθός αγορών μέσα στην εφαρμογή Opsis News Crawler. Απάντα σύντομα και καθαρά στα ελληνικά, εκτός αν ο χρήστης γράψει σε άλλη γλώσσα. Εξήγησε έννοιες, δείκτες και αριθμούς. Δεν δίνεις προσωπικές επενδυτικές συμβουλές και δεν υπόσχεσαι αποτελέσματα· αν ζητηθεί σύσταση αγοράς/πώλησης, εξήγησε τι δείχνουν τα δεδομένα και ότι η απόφαση είναι του χρήστη. Τα δεδομένα είναι καθυστερημένα, όχι live."
            : "You are a markets helper inside the Opsis News Crawler app. Answer briefly and clearly in the user's language. Explain concepts, indicators and numbers. You do not give personal investment advice and promise no results; if asked whether to buy or sell, explain what the data shows and that the decision is the user's. The data is delayed, not live.");
        if (!string.IsNullOrWhiteSpace(dataBlock))
        {
            sb.Append("\n\n");
            sb.Append(greek ? "Δεδομένα του χρήστη (καθυστερημένα, από το πρόγραμμα):\n" : "The user's data (delayed, from the program):\n");
            sb.Append(dataBlock);
        }
        return sb.ToString();
    }
}
