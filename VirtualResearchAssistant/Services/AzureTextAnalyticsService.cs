using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.AI.TextAnalytics;
using StudentResearchAssistant.Utils;

namespace StudentResearchAssistant.Services;

public class AzureTextAnalyticsService
{
    private readonly TextAnalyticsClient? _client;

    public AzureTextAnalyticsService()
    {
        if (!string.IsNullOrWhiteSpace(Settings.AzureTextEndpoint) && !string.IsNullOrWhiteSpace(Settings.AzureTextKey))
        {
            var endpoint = new Uri(Settings.AzureTextEndpoint!);
            var credential = new AzureKeyCredential(Settings.AzureTextKey!);
            _client = new TextAnalyticsClient(endpoint, credential);
        }
    }

    public bool IsEnabled => _client is not null;

    public async Task<IReadOnlyList<string>> ExtractKeyPhrasesAsync(string text)
    {
        if (_client is null) return Array.Empty<string>();
        var resp = await _client.ExtractKeyPhrasesAsync(text);
        return resp.Value.ToList();
    }
}
