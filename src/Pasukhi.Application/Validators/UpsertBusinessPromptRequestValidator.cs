using FluentValidation;
using Pasukhi.Application.DTOs.Ai;

namespace Pasukhi.Application.Validators;

public class UpsertBusinessPromptRequestValidator : AbstractValidator<UpsertBusinessPromptRequest>
{
    public UpsertBusinessPromptRequestValidator()
    {
        RuleFor(x => x.SystemPrompt).MaximumLength(4000);
        RuleFor(x => x.ToneDescription).MaximumLength(500);
        RuleFor(x => x.EscalationMessage).MaximumLength(500);
        RuleFor(x => x.MaxAiTokensPerDay).InclusiveBetween(1000, 1_000_000);
        RuleFor(x => x.AiConfidenceThreshold).InclusiveBetween(0.0, 1.0);
        RuleFor(x => x.FaqConfidenceThreshold).InclusiveBetween(0.0, 1.0);
    }
}
