using FluentValidation;
using Pasukhi.Application.DTOs.Rules;

namespace Pasukhi.Application.Validators;

public class UpdateRulePrioritiesRequestValidator : AbstractValidator<UpdateRulePrioritiesRequest>
{
    public UpdateRulePrioritiesRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Id).NotEmpty();
            item.RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        });
    }
}
