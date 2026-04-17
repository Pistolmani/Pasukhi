namespace Pasukhi.Domain.Entities;

public class BusinessPrompt : TenantEntity
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string ToneDescription { get; set; } = "professional and friendly";
    public string EscalationMessage { get; set; } = "Let me connect you with our team.";
    public int MaxAiTokensPerDay { get; set; } = 50000;
    public double AiConfidenceThreshold { get; set; } = 0.7;
    public double FaqConfidenceThreshold { get; set; } = 0.85;
    public bool IsAiEnabled { get; set; }
}
