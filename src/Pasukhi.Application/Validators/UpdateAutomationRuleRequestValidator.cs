using FluentValidation;
using Pasukhi.Application.DTOs.Rules;

namespace Pasukhi.Application.Validators;

public class UpdateAutomationRuleRequestValidator : AbstractValidator<UpdateAutomationRuleRequest>
{
    public UpdateAutomationRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TriggerValue).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.ActionValue).NotEmpty().MaximumLength(4000);
    }
}
