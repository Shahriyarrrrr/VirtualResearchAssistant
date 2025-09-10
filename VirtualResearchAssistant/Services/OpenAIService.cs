using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StudentResearchAssistant.Utils;

namespace StudentResearchAssistant.Services;

public class OpenAIService
{
    private readonly HttpClient _http = new();

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt)
    {
        var baseUrl = Settings.OpenAIApiBase ?? "https://api.openai.com";
        var isAzure = Settings.OpenAIApiBase is not null;

        var endpoint = isAzure
            ? $"{baseUrl}/openai/deployments/{Settings.OpenAIModelOrDeployment}/chat/completions?api-version=2024-06-01"
            : $"{baseUrl}/v1/chat/completions";

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var apiKey = Settings.OpenAIApiKey ?? throw new InvalidOperationException("OPENAI_API_KEY is not set.");
        if (isAzure) req.Headers.Add("api-key", apiKey);
        else req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var body = new
        {
            model = isAzure ? null : Settings.OpenAIModelOrDeployment,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.3,
            max_tokens = 1000
        };

        req.Content = new StringContent(JsonSerializer.Serialize(body,
            new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }),
            Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    public string BuildSummaryPrompt(string text) =>
        $"You are an academic assistant. Summarize the following content in 5-8 bullet points:\n\n{text}";

    public string BuildQuizPrompt(string text) =>
        """
        Create 8 exam-quality questions from the following study material. Mix: 4 MCQs (A–D, include Answer: X),
        2 short-answer, 2 explain prompts.

        Material:
        """ + text;

    public string BuildQAWithContextPrompt(string question, string context) =>
        $"Answer strictly from CONTEXT. If not present, say you couldn't find it.\n\nQ:\n{question}\n\nCONTEXT:\n{context}";
}
