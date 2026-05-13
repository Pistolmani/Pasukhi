namespace Pasukhi.Domain.Entities;

public class BotQuestionnaireAnswer : TenantEntity
{
    public string QuestionKey { get; set; } = string.Empty;
    public string? AnswerText { get; set; }
    public bool IsSkipped { get; set; }
}
