using FluentValidation;
using Pasukhi.Application.DTOs.Escalations;

namespace Pasukhi.Application.Validators;

public class ResolveEscalationRequestValidator : AbstractValidator<ResolveEscalationRequest>
{
    public ResolveEscalationRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}
