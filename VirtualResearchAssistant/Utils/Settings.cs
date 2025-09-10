using System;

namespace StudentResearchAssistant.Utils;

public static class Settings
{
    public static string AppDataDir =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StudentResearchAssistant");
    public static string IndexDir => System.IO.Path.Combine(AppDataDir, "index");

    public static string? OpenAIApiKey => Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    public static string? OpenAIApiBase => Environment.GetEnvironmentVariable("OPENAI_API_BASE");
    public static string OpenAIModelOrDeployment =>
        Environment.GetEnvironmentVariable("OPENAI_API_MODEL") ?? "gpt-4o-mini";

    public static string? AzureTextEndpoint => Environment.GetEnvironmentVariable("AZURE_TEXT_ENDPOINT");
    public static string? AzureTextKey => Environment.GetEnvironmentVariable("AZURE_TEXT_KEY");
}
