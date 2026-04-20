namespace Pasukhi.Application.AI;

public class AiOptions
{
    public string Provider { get; set; } = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash-lite"; // "gpt-5-mini" for OpenAI, "gemini-2.0-flash-lite" for Gemini
    public int MaxTokens { get; set; } = 500;
    public double Temperature { get; set; } = 0.3;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public string ReasoningEffort { get; set; } = "low";

    /// <summary>
    /// Returns the default model for the configured provider if Model is not set.
    /// </summary>
    public string GetDefaultModel()
    {
        if (!string.IsNullOrWhiteSpace(Model))
            return Model;

        return Provider.ToLowerInvariant() switch
        {
            "gemini" or "google" => "gemini-2.0-flash-lite",
            "openai" or _ => "gpt-5-mini"
        };
    }
}
