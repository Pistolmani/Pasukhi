namespace Pasukhi.Application.DTOs.Ai;

public record BusinessPromptDto(
    Guid Id,
    bool IsAiEnabled,
    string SystemPrompt,
    string ToneDescription,
    string EscalationMessage,
    int MaxAiTokensPerDay,
    double AiConfidenceThreshold,
    double FaqConfidenceThreshold);

public record UpsertBusinessPromptRequest(
    bool IsAiEnabled,
    string SystemPrompt,
    string ToneDescription,
    string EscalationMessage,
    int MaxAiTokensPerDay,
    double AiConfidenceThreshold,
    double FaqConfidenceThreshold);
